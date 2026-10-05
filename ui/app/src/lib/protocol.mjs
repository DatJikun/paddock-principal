export const HUMAN_MANAGER_ID = 'human:1';

export const BLOCKING_KEY = 'ready.blockingItem';

/** A host reply or a push. Pushes carry type and no id; replies carry id and ok. */
export function classify(raw) {
  const message = typeof raw === 'string' ? JSON.parse(raw) : raw;
  if (!message || typeof message !== 'object') return { kind: 'invalid' };
  if (typeof message.type === 'string' && message.id === undefined) {
    return { kind: 'event', type: message.type, data: message.data ?? null };
  }
  if (typeof message.id === 'string' && typeof message.ok === 'boolean') {
    if (message.ok) return { kind: 'reply', id: message.id, ok: true, data: message.data ?? null };
    const error = message.error ?? {};
    return {
      kind: 'reply',
      id: message.id,
      ok: false,
      error: {
        key: typeof error.key === 'string' ? error.key : 'bridge.error.badMessage',
        parameters: error.parameters && typeof error.parameters === 'object' ? error.parameters : {},
      },
    };
  }
  return { kind: 'invalid' };
}

export function isTimeBlocked(error) {
  return !!error && error.key === BLOCKING_KEY;
}

/** Dalej advances only when nothing is holding the clock. A known decision opens that item. */
export function nextAction(shell) {
  if (shell && shell.decisionItemId) {
    return { type: 'show', screen: 'skrzynka', itemId: shell.decisionItemId };
  }
  return { type: 'advance' };
}

export function afterAdvance(reply) {
  if (reply.ok) return { type: 'advanced', date: reply.data?.date ?? null };
  if (isTimeBlocked(reply.error)) return { type: 'show', screen: 'skrzynka' };
  return { type: 'error', key: reply.error?.key ?? 'bridge.error.badMessage' };
}
