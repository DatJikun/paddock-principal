# Paddock Principal — ROADMAP

**Status:** aktualne na 2026-09-26
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
- [ ] System komponentów po uwagach: zakładki/przełącznik, status, segmentowane pola, `plural()`, flagi SVG, cięższe przejścia, „Potwierdź”.
- [ ] Przebudowa ekranów z listy w HANDOFF_UI §6.
- Po fazie 5 z tych komponentów składamy prawdziwe ekrany (faza 6).

## Faza 1: Pipeline danych historycznych (test wykonalności)
Najbardziej ryzykowna część całego pomysłu, więc robimy ją pierwszą.

**Stan (2026-09-26):**
- [x] #1 T1: solucja .NET 10, deterministyczny generator losowości (Xoshiro256**, strumienie), CI. Zmergowane.
- [x] #2 R1: katalog 41 wymiarów regulaminu i oś czasu 1950–2026 (`data/authored/regulations/`). Zmergowane.
- [x] #3 R2: 78 torów, 156 wersji układów, 1172 wyścigi przypisane do układów (`data/authored/tracks/`). Zmergowane.
- [ ] #4 T2: importer Jolpica-F1 (PR #8). Czeka na zakończenie pobierania (limit API ~450 zapytań/h) i wynik `summary`.
- [ ] Model ocen kierowców (Claude), patrz niżej.
- [ ] Oś czasu epok, drzewo technologii, kluczowy personel (`data/authored/`).
- [ ] Raport do oceny właściciela (bramka).

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

**Bramka:** 1950→2026 w SimRunnerze. Ludzie pojawiają się, starzeją i odchodzą, zapis pozostaje mały, a wynik jest deterministyczny.

## Faza 3: Silnik wyścigu dla wielu epok
- Model okrążeń parametryzowany epoką, strategia sztabu, awaryjność, pogoda, incydenty i kontuzje.
- Race Spy od pierwszego dnia.
- Test wierności historii: 1950–1960, a potem kolejne dekady.

**Bramka (grywalności):** czytasz relację wyścigu z 1955 i z 1988. Czuć różnicę epok, a wyniki są wiarygodne.

## Faza 4: Pętla kariery (pierwszy grywalny sezon, w CLI)
- Start kariery: praca w istniejącym zespole albo **własny zespół z pakietem sponsora założycielskiego** (PP-015).
- Konfiguracja kariery: presety, siła historii, los legend (DESIGN §2.3).
- Rynek i negocjacje, pula talentów, projekt samochodu i R&D, drzewo technologii, własne silniki, finanse i sponsorzy.
- AI szefów zespołów, zwolnienia (także gracza).
- Powstawanie, upadki i wykupy zespołów; propozycje historyczne.
- Projekt szczegółowy: zakładanie i wykup zespołu w trakcie kariery.

**Bramka:** pełny sezon 1955 od A do Z, w którym decyzje mają odczuwalne konsekwencje.

## Faza 5: Żywa historia
- Kronika rozbieżności, Hall of Fame, rekordy, kompaktowanie historii.
- Skrzynka i zdarzenia życiowe.
- Przejście od prawdziwych ludzi do generatora po 2026.

**Bramka:** kariera 1950→2040 w SimRunnerze plus Twoja ręczna rozgrywka kilku sezonów. Historia rozjeżdża się ciekawie, a nie losowo.

## Faza 6: UI (HTML/TS/Svelte w Photino)
- Most JSON, tryb deweloperski w przeglądarce, zrzuty ekranu do przeglądu.
- Prawdziwe ekrany z design systemu (tor równoległy): gęste tabele, ekran wyścigu, kronika, onboarding.

## Faza 7+: Rozszerzenia
Serie juniorskie, Le Mans / WEC / GT (wizja endurance i zasady wejścia na wyścigi 24h z wcześniejszych ustaleń), tryb proceduralny od zera, wyzwania, edytor bazy, paczka fikcyjna, wydanie.

---

## Otwarte pytania

1. **Rozwój auta: czy gracz wybiera konkretne projekty, czy tylko podział zasobów?** Propozycja właściciela: projekty wybierają inżynierowie, a gracz dostaje w skrzynce prośby typu „jeszcze 2 tygodnie, jesteśmy blisko przełomu” (HANDOFF_UI §4). Do decyzji przed przebudową ekranu „Auto i rozwój”.
2. **Czy szef zespołu, gracz i AI, ma atrybuty?** Obecnie DESIGN §6.2 mówi „tak”, a właściciel pyta, czy to potrzebne.
3. **Dane do wydania komercyjnego (PP-041):** Jolpica to CC BY-NC-SA 4.0. Na Steam trzeba własnej bazy albo zgody. Decyzja do podjęcia przed fazą wydania.
4. **Kalibracja ocen:** ile lat przed debiutem kierowca trafia do puli talentów (fazy 1 i 4).

Rozstrzygnięte 2026-09-26: technologie jako przełomy od ludzi, bez drzewka (PP-042).

Rozstrzygnięte 2026-09-25: wiedza o przyszłości (PP-018), tylko mistrzostwa F1 na start (PP-018), własne silniki (PP-019), backend przed UI (PP-020), dwa języki (PP-021), publiczne repo (PP-022).
