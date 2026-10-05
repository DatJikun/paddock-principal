import type { BridgeCommandMap, BridgeCommandName, BridgeQueryMap, BridgeQueryName } from './types.generated';
import { classify, HUMAN_MANAGER_ID } from '../protocol.mjs';

export { HUMAN_MANAGER_ID };

export class BridgeError extends Error {
  readonly key: string;
  readonly parameters: Record<string, string>;

  constructor(key: string, parameters: Record<string, string> = {}) {
    super(key);
    this.key = key;
    this.parameters = parameters;
  }
}

type Waiter = {
  resolve: (data: unknown) => void;
  reject: (error: BridgeError) => void;
};

export type PushHandler = (type: string, data: unknown) => void;

interface PhotinoHost {
  sendMessage(message: string): void;
  receiveMessage(callback: (message: string) => void): void;
}

let socket: WebSocket | null = null;
let opened: Promise<void> | null = null;
let seq = 0;
const pending = new Map<string, Waiter>();
let onPush: PushHandler = () => {};

function photino(): PhotinoHost | null {
  const host = (window as unknown as { external?: PhotinoHost }).external;
  if (host && typeof host.sendMessage === 'function' && typeof host.receiveMessage === 'function') return host;
  return null;
}

export function bridgeUrl(): string {
  const fromEnv = import.meta.env.VITE_BRIDGE_WS;
  if (fromEnv) return fromEnv;
  if (location.protocol.startsWith('http') && location.hostname === '127.0.0.1' && location.port !== '5173') {
    const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
    return `${proto}//${location.host}/bridge`;
  }
  return 'ws://127.0.0.1:4731/bridge';
}

function deliver(raw: string): void {
  let message: ReturnType<typeof classify>;
  try {
    message = classify(raw);
  } catch {
    return;
  }
  if (message.kind === 'event') {
    onPush(message.type, message.data);
    return;
  }
  if (message.kind !== 'reply') return;
  const waiter = pending.get(message.id);
  if (!waiter) return;
  pending.delete(message.id);
  if (message.ok) waiter.resolve(message.data);
  else {
    const error = message.error ?? { key: 'bridge.error.badMessage', parameters: {} };
    waiter.reject(new BridgeError(error.key, error.parameters));
  }
}

export function connect(handler: PushHandler): () => void {
  onPush = handler;
  const host = photino();
  if (host) {
    host.receiveMessage(deliver);
    opened = Promise.resolve();
    return () => {};
  }
  const ws = new WebSocket(bridgeUrl());
  socket = ws;
  opened = new Promise((resolve, reject) => {
    ws.addEventListener('open', () => resolve(), { once: true });
    ws.addEventListener('error', () => reject(new BridgeError('bridge.error.internal')), { once: true });
  });
  ws.addEventListener('message', (event) => deliver(String(event.data)));
  return () => ws.close();
}

export function ready(): Promise<void> {
  return opened ?? Promise.reject(new BridgeError('bridge.error.internal'));
}

function call(kind: 'query' | 'command', name: string, args: object): Promise<unknown> {
  const id = String(++seq);
  const body = JSON.stringify({ kind, name, args, id });
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    const host = photino();
    if (host) {
      host.sendMessage(body);
      return;
    }
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      pending.delete(id);
      reject(new BridgeError('bridge.error.internal'));
      return;
    }
    socket.send(body);
  });
}

export function query<N extends BridgeQueryName>(
  name: N,
  args: BridgeQueryMap[N]['args'],
): Promise<BridgeQueryMap[N]['result']> {
  return call('query', name, args) as Promise<BridgeQueryMap[N]['result']>;
}

export function command<N extends BridgeCommandName>(
  name: N,
  args: BridgeCommandMap[N]['args'],
): Promise<BridgeCommandMap[N]['result']> {
  return call('command', name, args) as Promise<BridgeCommandMap[N]['result']>;
}

/** True when the page runs inside the desktop window, which is the only place "Quit" can close anything. */
export function canExit(): boolean {
  return photino() !== null;
}

/** Closes the desktop window. A window message of its own, not a bridge name: the game never sees it. */
export function exitApp(): void {
  photino()?.sendMessage(JSON.stringify({ kind: 'window', name: 'exit' }));
}
