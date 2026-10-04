# Paddock Principal — ROADMAP

**Status:** aktualne na 2026-10-04
**Zasada:** każda faza kończy się bramką, czyli czymś, co da się uruchomić i ocenić. Poza fazą 0 nie ma faz „tylko dokumentacja”.

---

## Faza 0: Fundament ✅
- [x] VISION z decyzjami, DESIGN, TECH, ROADMAP; stare dokumenty skonsolidowane (są w historii gita)
- [x] repozytorium git
- [x] push na GitHub (github.com/DatJikun/paddock-principal)
- [x] odpowiedzi na otwarte pytania

## Tor równoległy: język wizualny (od fazy 1, PP-023)
- [x] 3 kierunki (A „ściana boksu”, B „gazeta”, C „barwy epoki”). Wybrany **C**, a B stał się pomysłem na gazetę w grze (PP-038, PP-039).
- [x] Pulpit w 5 iteracjach (`ui/mockups/`), potem **klikalny prototyp wszystkich 21 ekranów** (`ui/prototype/`).
- [x] Pełna runda uwag właściciela do prototypu: **`ui/HANDOFF_UI.md`** (zasady plus uwagi ekran po ekranie). **Następny krok UI zaczyna się od tego pliku.**
- [x] System komponentów po uwagach: zakładki/przełącznik, status, segmentowane pola, `plural()`, flagi SVG, cięższe przejścia, „Potwierdź” (szkicowo, HANDOFF_UI §8).
- [x] Przebudowa ekranów z listy w HANDOFF_UI §6 (szkicowo, HANDOFF_UI §8). Auto i rozwój czekają na decyzję właściciela.
- Po fazie 5 z tych komponentów składamy prawdziwe ekrany (faza 6).

## Faza 1: Pipeline danych historycznych (test wykonalności)
Najbardziej ryzykowna część całego pomysłu, więc robimy ją pierwszą.

**Stan (2026-10-04):**
- [x] T1–T12 zmergowane: solucja .NET 10 z deterministyczną losowością i CI, importer Jolpica-F1, loadery danych autorskich, szkielet zapisu SQLite, statystyki epok, oceny v0, harmonogram historyczny ludzi (T12). Research (regulaminy, tory, technologie, personel, zdarzenia zespołów, ofiary, pogoda) jest w `data/authored/`.
- [x] T22: oceny v1 (efekt auta, krzywe kariery, ocena ogólna, gwiazdki), #99.
- [ ] Pełne pobieranie Jolpica (limit API ~450 zapytań/h) na maszynie właściciela.
- [ ] `dotnet run --project tools/Paddock.DataPipeline -- ratings` na prawdziwych danych i kalibracja.
- [ ] Raport do oceny właściciela (bramka).
- Zadania fazy 2 zaczęte wcześniej (decyzja właściciela, PP-046) są zmergowane, patrz faza 2.

**Model ocen: plan metody** (do zrobienia przez Claude'a, nie Groka):
1. Dla każdego wyścigu i kwalifikacji: wynik względny kierowcy wobec partnera z zespołu (różnica pozycji albo czasu, gdy jest dostępny), z odrzuceniem awarii, które nie są winą kierowcy.
2. Model: *wynik = umiejętność kierowcy w sezonie + efekt auta (konstruktor × sezon) + szum*, liczony regularyzowaną regresją po całej sieci partnerów. Łańcuchy partnerów łączą epoki, np. Fangio–Moss–Clark–…
3. Krzywa kariery: wygładzona umiejętność rok po roku, z której wychodzą szczyt, sufit i tempo spadku.
4. Przełożenie na grę: atrybuty 1–20 i gwiazdki (PP-040). Cechy i powinowactwo do torów biorą się z tego, co zostaje po odjęciu modelu (reszty).
5. Raport: ranking wszech czasów i per dekada, niepewność (mało wyścigów, mało partnerów) oraz lista miejsc, gdzie model się myli, z wyjaśnieniem.
- `DataPipeline`: import F1 1950–2025 z Jolpica-F1 (kierowcy, konstruktorzy, tory, wyniki, kwalifikacje) do lokalnego cache.
- Model ocen: porównania z partnerem z zespołu i efekt konstruktor × sezon, co daje tempo na sezon, krzywą kariery i sufit talentu.
- Przypadki brzegowe: Indy 500 w latach 1950–60, kierowcy jednego wyścigu, dzielone samochody.
- `data/authored/`: szkielet osi czasu epok, drzewa technologii i kluczowego personelu (szefowie i projektanci czołowych zespołów każdej dekady).
- **Research dla Groka (R1):** katalog zasad sportowych i technicznych F1 1950–2026, rok po roku, ze źródłami, plus katalog nietypowych zasad z innych serii i gier (np. Motorsport Manager). Wynik to dane w `data/authored/regulations/`, a nie dokument.
- **Research dla Groka (R2):** tory F1 1950–2026 z wersjami układu (lata, długość, charakter).
- **Raport do Twojej oceny:** ranking kierowców wszech czasów i per dekada, z listą miejsc, gdzie model się myli, i wyjaśnieniem dlaczego.

**Bramka:** przeglądasz raport i mówisz „to ma sens”. Jeśli się nie da, zmieniamy podejście, zanim powstanie reszta gry.

## Faza 2: Rdzeń świata
- Solucja .NET 10 (TECH §2); przeniesienie z Pelotona determinizmu, RNG, zapisu SQLite, migracji i kalendarza.
- Osoby, organizacje (z linią następstwa), tory z wersjami, kontrakty, oś czasu epok, harmonogram ludzi, tick dnia.
- SimRunner: przebieg bez wyścigów.
- Pod multiplayer (PP-045): komendy i widoki z `managerId`, bramka gotowości przed `AdvanceDay`, jedna kolejka komend. Sieci jeszcze nie ma.

**Stan (2026-10-04):**
- [x] T15 domena świata, T16 tick dnia, T17 kolejka komend z bramką gotowości, T18 SimRunner (`run`), T19 zapis encji świata, T20 inicjalizator świata, T21 `CareerConfig`, T13 generator ludzi.
- [x] Wznowienie zapisu daje identyczną przyszłość (#125); emeryci zostają w świecie z datą odejścia (#120).
- [ ] Bramka: przebieg 1950→2026 jest deterministyczny na wygenerowanych ludziach. Przebieg na prawdziwych danych czeka na cache Jolpica.

**Bramka:** 1950→2026 w SimRunnerze. Ludzie pojawiają się, starzeją i odchodzą, zapis pozostaje mały, a wynik jest deterministyczny.

## Faza 3: Silnik wyścigu dla wielu epok
- Model okrążeń parametryzowany epoką, strategia sztabu, awaryjność, pogoda, incydenty i kontuzje.
- Race Spy od pierwszego dnia.
- Wyścig jako strumień zdarzeń, który da się odtwarzać w tempie oglądania (potrzebne do wspólnego oglądania online, PP-045).
- Test wierności historii: 1950–1960, a potem kolejne dekady.

**Stan (2026-10-04):**
- [x] T26–T34: strumień zdarzeń, punkty i klasyfikacja, kwalifikacje, model okrążeń, opony i paliwo, awaryjność, pogoda, incydenty, pit stopy ze strategiem; Race Spy (T24) i głosowane przepisy (T23) już wcześniej.
- [x] T35: orkiestrator weekendu wyścigowego (#119).
- [x] Czytelna relacja wyścigu do bramki (#124).
- [ ] Kalibracja silnika z historią: pit stopy, mokre opony, wycofania (#122).

**Bramka (grywalności):** czytasz relację wyścigu z 1955 i z 1988. Czuć różnicę epok, a wyniki są wiarygodne.

## Faza 4: Pętla kariery (pierwszy grywalny sezon, w CLI)
- Start kariery: praca w istniejącym zespole albo **własny zespół z pakietem sponsora założycielskiego** (PP-015).
- Konfiguracja kariery: presety, siła historii, los legend (DESIGN §2.3).
- Rynek i negocjacje, pula talentów, projekt samochodu i R&D, drzewo technologii, własne silniki, finanse i sponsorzy.
- AI szefów zespołów, zwolnienia (także gracza).
- Powstawanie, upadki i wykupy zespołów; propozycje historyczne.
- Projekt szczegółowy: zakładanie i wykup zespołu w trakcie kariery.

**Stan (2026-10-04):** zadania T35–T48 (#100–#113) są założone jako issues.
- [x] Zmergowane: T35 weekend wyścigowy (#119), T36 fundamenty: rejestr sekcji świata, skrzynka v0, cele (#117), T39 kontrakty i negocjacje (#128), T40 pula talentów (#127).
- [ ] W przeglądzie: wpięcie T39 i T40 w przebieg kariery (#129, PR #134).
- [ ] Do zrobienia, wszystkie odblokowane decyzjami z PP-050: T37 finanse (#102, od niego zależą T38 i T45), T41 model auta (#106, niezależny), T38 sponsorzy (#103), T45 reputacja i zarząd (#110), T46 cykl życia zespołów, zakres zmniejszony (#111).
- [ ] Kolejne w kolejności zależności: T42 rozwój, ścieżka A (#107), T43 dostawcy i silniki (#108), T44 AI szefów zespołów (#109), T47 start kariery i pętla sezonu w CLI (#112), T48 scenariusz bramki, sezon 1955 (#113).
- [ ] Poza kolejką T-zadań: kalibracja silnika (#122, wymaga cache Jolpica), odporność geometrii torów na różne systemy (#141), szybki domyślny zestaw testów (#142), `race-replay` na prawdziwej taśmie (#143), prędkość i czas okrążenia z fizyki (#138, PR #139).

**Bramka:** pełny sezon 1955 od A do Z, w którym decyzje mają odczuwalne konsekwencje.

**Zakres MVP (PP-050, PP-052):** przejęty istniejący zespół, dwa auta i dwóch kierowców na zespół, trzy miejsca na sponsora. Własny zespół, sponsor założycielski, zakładanie i wykup zespołu są po MVP. Uproszczenie „dwa auta, dwóch kierowców, bez prywatnych zespołów i sprzedaży podwozi” jest tylko ustawieniem MVP, a nie ograniczeniem modelu (PP-052).

## Faza 5: Żywa historia
- Kronika rozbieżności, Hall of Fame, rekordy, kompaktowanie historii.
- Skrzynka i zdarzenia życiowe.
- Przejście od prawdziwych ludzi do generatora po 2026.

**Bramka:** kariera 1950→2040 w SimRunnerze plus Twoja ręczna rozgrywka kilku sezonów. Historia rozjeżdża się ciekawie, a nie losowo.

## Faza 6: UI (HTML/TS/Svelte w Photino)
- Most JSON, tryb deweloperski w przeglądarce, zrzuty ekranu do przeglądu.
- Prawdziwe ekrany z design systemu (tor równoległy): gęste tabele, ekran wyścigu, kronika, onboarding.
- **Multiplayer online (PP-045):** host i goście przez WebSocket, wspólna data, wspólne oglądanie wyścigu na żywo.

**Bramka:** Ty i kolega rozgrywacie razem sezon przez internet, każdy swoim zespołem.

## Faza 7+: Rozszerzenia
- Z przeglądu Team Principal (PP-048), po MVP: specjalizacja projektantów (wolne / średnie / szybkie zakręty, opór) i kierunek rozwoju jako dźwignia gracza.
- Rywalizacje z kolizji na torze oraz przyjaźń i szacunek z wcześniejszych wspólnych startów w seriach juniorskich (PP-049).
- Scenariusze startowe: własna baza startowa wybranego sezonu i wydarzenia oskryptowane w czasie (PP-051).
Serie juniorskie, Le Mans / WEC / GT (wizja endurance i zasady wejścia na wyścigi 24h z wcześniejszych ustaleń), tryb proceduralny od zera, wyzwania, edytor bazy, paczka fikcyjna, wydanie.

---

## Otwarte pytania

1. ~~Czy właściciel albo zarząd mają atrybuty?~~ Rozstrzygnięte w PP-050: na razie nie, jest skalar cierpliwości. Szef zespołu ma atrybuty (PP-044).
2. **System awatarów: odłożony.** Dotychczasowe próby (Peloton, Ping-Pong, brief w HANDOFF_UI §7) nie dały zadowalającego wyniku. Wracamy później.
3. **Dane do wydania komercyjnego (PP-041):** Jolpica to CC BY-NC-SA 4.0. Na Steam trzeba własnej bazy albo zgody. Decyzja do podjęcia przed fazą wydania.
4. **Kalibracja ocen:** ile lat przed debiutem kierowca trafia do puli talentów (fazy 1 i 4).

Rozstrzygnięte 2026-09-26: technologie jako przełomy od ludzi, bez drzewka (PP-042); rozwój auta z dwiema ścieżkami, MVP z autonomicznymi inżynierami (PP-043); szef zespołu ma atrybuty (PP-044).

Rozstrzygnięte 2026-09-25: wiedza o przyszłości (PP-018), tylko mistrzostwa F1 na start (PP-018), własne silniki (PP-019), backend przed UI (PP-020), dwa języki (PP-021), publiczne repo (PP-022).
