# Paddock Principal

Manager motorsportu, w którym możesz zacząć karierę w 1950 roku, prowadzić zespół przez całą historię F1 z prawdziwymi ludźmi i zmienić jej bieg. Po 2026 świat żyje dalej proceduralnie, bez końca.

**Stan (2026-10-04):** fazy 1–3 w większości zrobione, faza 4 w toku. Szczegóły w [ROADMAP.md](ROADMAP.md).
- Rdzeń świata działa: SimRunner przechodzi 1950→2026 deterministycznie, na prawdziwych i na wygenerowanych ludziach, a zapis się wznawia. Bez AI zatrudniającego kierowców świat jednak wymiera (T44).
- Oceny kierowców są policzone na pełnych danych Jolpica 1950–2025 i trafiają do świata kariery. Bramka fazy 1 czeka na decyzję właściciela (#131).
- Silnik wyścigu jest gotowy (weekend wyścigowy z relacją). Druga runda kalibracji z historią jest do zrobienia.
- Moduły fazy 4 (finanse, sponsorzy, auto, rozwój, dostawy, zarząd) są gotowe, ale pętla kariery jeszcze ich nie uruchamia (#160).
- Klikalny prototyp UI w wersji szkicowej: `ui/prototype/`, uwagi w `ui/HANDOFF_UI.md`.

**Dane historyczne lokalnie:** cache Jolpica jest w prywatnym repo `paddock-data` (licencja CC BY-NC-SA, PP-041). Skopiuj jego `jolpica/` do `data/cache/jolpica/`, potem uruchom `dotnet run --project tools/Paddock.DataPipeline -- ratings` i `-- schedule`.

## Dokumentacja

| Plik | Co zawiera |
|---|---|
| [VISION.md](VISION.md) | kierunek, filary i wszystkie przyjęte decyzje (PP-xxx) |
| [ROADMAP.md](ROADMAP.md) | fazy z bramkami i otwarte pytania |
| [DESIGN.md](DESIGN.md) | systemy gry: świat, historia, epoki, samochód, ludzie, wyścig, AI, ekonomia |
| [TECH.md](TECH.md) | stack, architektura, determinizm, dane, zapis, diagnostyka, testy |
| [AGENTS.md](AGENTS.md) | zasady dla agentów AI pomagających w repo |
| [ui/HANDOFF_UI.md](ui/HANDOFF_UI.md) | stan prototypu UI i pełne uwagi właściciela |

Dokumentacji ma być mało (PP-017). Szczegóły żyją w kodzie i historii gita.

## Stack

C# / .NET 10 (rdzeń symulacji) · SQLite (zapisy) · HTML/CSS + TypeScript + Svelte w oknie Photino/WebView2 (UI). Uzasadnienie: [TECH.md §1](TECH.md#1-stack).
