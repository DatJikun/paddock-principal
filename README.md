# Paddock Principal

Manager motorsportu, w którym możesz zacząć karierę w 1950 roku, prowadzić zespół przez całą historię F1 z prawdziwymi ludźmi i zmienić jej bieg. Po 2026 świat żyje dalej proceduralnie, bez końca.

**Stan (2026-10-04):** fazy 1–3 w większości zrobione, faza 4 w toku.
- Rdzeń świata działa: SimRunner przechodzi 1950→2026 deterministycznie na wygenerowanych ludziach, zapis się wznawia.
- Silnik wyścigu jest gotowy (weekend wyścigowy z relacją); kalibracja z historią czeka.
- Czekają pełne dane Jolpica na maszynie właściciela i oceny na prawdziwych danych.
- Pętla kariery (faza 4) jest w toku, część zadań czeka na decyzje właściciela.
- Klikalny prototyp UI w wersji szkicowej: `ui/prototype/`, uwagi w `ui/HANDOFF_UI.md`.

## Jak uruchomić grę (Windows)

Gra jest w wersji do testów i nie ma instalatora. Dostajesz zwykły plik zip z programem, w którym jest wszystko, co potrzebne. Nie musisz instalować .NET ani niczego budować.

1. Pobierz zip. Są dwa miejsca:
   - **Wydanie na GitHubie:** zakładka Releases w repozytorium, plik `PaddockPrincipal-…-win-x64-….zip`. Pojawia się tylko przy oznaczonych wersjach (tagi `v…`).
   - **Zakładka Actions:** w workflow „Windows release” wybierz ostatni zielony przebieg i pobierz plik z sekcji Artifacts. GitHub pakuje go jeszcze raz, więc po pobraniu rozpakuj najpierw tę zewnętrzną paczkę. Nowy build uruchomisz tam przyciskiem „Run workflow”. W nazwie jest data i skrót commita, więc wiadomo, który to build.
2. Rozpakuj cały zip do zwykłego folderu (na przykład `C:\Gry\PaddockPrincipal`, nie do Program Files). Zapisy gry trafiają do folderu `saves` obok programu.
3. Kliknij dwukrotnie `PaddockPrincipal.exe`. Okno gry otwiera się samo.
4. Jeśli Windows pokaże ostrzeżenie SmartScreen, to dlatego, że program nie jest podpisany. Kliknij „Więcej informacji”, a potem „Uruchom mimo to”.

Potrzebny jest Windows 10 lub 11 (64-bit) z WebView2, który system ma domyślnie. W paczce są tylko dane własne gry, bez danych historycznych z zewnętrznych źródeł, więc ludzie w świecie są generowani. To samo, krócej, jest w pliku `README.txt` w środku zipa.

## Dokumentacja

| Plik | Co zawiera |
|---|---|
| [GUIDE.md](GUIDE.md) | przewodnik dla testerów: co wybierasz w grze, jak to działa, wykresy i pytania o opinię |
| [VISION.md](VISION.md) | kierunek, filary i wszystkie przyjęte decyzje (PP-xxx) |
| [ROADMAP.md](ROADMAP.md) | fazy z bramkami i otwarte pytania |
| [DESIGN.md](DESIGN.md) | systemy gry: świat, historia, epoki, samochód, ludzie, wyścig, AI, ekonomia |
| [TECH.md](TECH.md) | stack, architektura, determinizm, dane, zapis, diagnostyka, testy |
| [AGENTS.md](AGENTS.md) | zasady dla agentów AI pomagających w repo |
| [ui/HANDOFF_UI.md](ui/HANDOFF_UI.md) | stan prototypu UI i pełne uwagi właściciela |

Dokumentacji ma być mało (PP-017). Szczegóły żyją w kodzie i historii gita.

**Wersja HTML (PP-056):** `node tools/docs/build-docs.mjs`, potem otwórz `build/docs/index.html`. Strony powstają z plików .md, a liczby i wykresy w przewodniku są czytane z kodu gry.

## Stack

C# / .NET 10 (rdzeń symulacji) · SQLite (zapisy) · HTML/CSS + TypeScript + Svelte w oknie Photino/WebView2 (UI). Uzasadnienie: [TECH.md §1](TECH.md#1-stack).
