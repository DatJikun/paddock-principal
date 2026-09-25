# Paddock Principal

Manager motorsportu, w którym możesz zacząć karierę w 1950 roku, prowadzić zespół przez całą historię F1 z prawdziwymi ludźmi i zmienić jej bieg. Po 2026 świat żyje dalej proceduralnie, bez końca.

**Stan:** planowanie zakończone, zaczyna się faza 1 (pipeline danych historycznych). Kodu jeszcze nie ma.

## Dokumentacja

| Plik | Co zawiera |
|---|---|
| [VISION.md](VISION.md) | kierunek, filary i wszystkie przyjęte decyzje (PP-xxx) |
| [ROADMAP.md](ROADMAP.md) | fazy z bramkami i otwarte pytania |
| [DESIGN.md](DESIGN.md) | systemy gry: świat, historia, epoki, samochód, ludzie, wyścig, AI, ekonomia |
| [TECH.md](TECH.md) | stack, architektura, determinizm, dane, zapis, diagnostyka, testy |
| [AGENTS.md](AGENTS.md) | zasady dla agentów AI pomagających w repo |

Dokumentacji ma być mało (PP-017). Szczegóły żyją w kodzie i historii gita.

## Stack

C# / .NET 10 (rdzeń symulacji) · SQLite (zapisy) · HTML/CSS + TypeScript + Svelte w oknie Photino/WebView2 (UI). Uzasadnienie: [TECH.md §1](TECH.md#1-stack).
