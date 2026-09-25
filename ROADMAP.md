# Paddock Principal — ROADMAP

**Status:** DRAFT (2026-09-25)
**Zasada:** każda faza kończy się bramką, czyli czymś, co da się uruchomić i ocenić. Poza fazą 0 nie ma faz „tylko dokumentacja”.

---

## Faza 0: Fundament ✅ (prawie)
- [x] VISION z decyzjami, DESIGN, TECH, ROADMAP; stare dokumenty skonsolidowane (są w historii gita)
- [x] repozytorium git
- [ ] push na GitHub
- [ ] odpowiedzi na [otwarte pytania](#otwarte-pytania)

## Faza 1: Pipeline danych historycznych (test wykonalności)
Najbardziej ryzykowna część całego pomysłu, więc robimy ją pierwszą.
- `DataPipeline`: import F1 1950–2025 z Jolpica-F1 (kierowcy, konstruktorzy, tory, wyniki, kwalifikacje) do lokalnego cache.
- Model ocen: porównania z partnerem z zespołu i efekt konstruktor × sezon, co daje tempo na sezon, krzywą kariery i sufit talentu.
- Przypadki brzegowe: Indy 500 w latach 1950–60, kierowcy jednego wyścigu, dzielone samochody.
- `data/authored/`: szkielet osi czasu epok, drzewa technologii i kluczowego personelu (szefowie i projektanci czołowych zespołów każdej dekady).
- **Raport do Twojej oceny:** ranking kierowców wszech czasów i per dekada, z listą miejsc, gdzie model się myli, i wyjaśnieniem dlaczego.

**Bramka:** przeglądasz raport i mówisz „to ma sens”. Jeśli się nie da, zmieniamy podejście, zanim powstanie reszta gry.

## Faza 2: Rdzeń świata
- Solucja .NET 9 (TECH §2); przeniesienie z Pelotona determinizmu, RNG, zapisu SQLite, migracji i kalendarza.
- Osoby, organizacje (z linią następstwa), tory z wersjami, kontrakty, oś czasu epok, harmonogram ludzi, tick dnia.
- SimRunner: przebieg bez wyścigów.

**Bramka:** 1950→2026 w SimRunnerze. Ludzie pojawiają się, starzeją i odchodzą, zapis pozostaje mały, a wynik jest deterministyczny.

## Faza 3: Silnik wyścigu dla wielu epok
- Model okrążeń parametryzowany epoką, strategia sztabu, awaryjność, pogoda, incydenty i kontuzje.
- Race Spy od pierwszego dnia.
- Test wierności historii: 1950–1960, a potem kolejne dekady.

**Bramka (grywalności):** czytasz relację wyścigu z 1955 i z 1988. Czuć różnicę epok, a wyniki są wiarygodne.

## Faza 4: Pętla kariery (pierwszy grywalny sezon, w CLI / prostym UI)
- Start kariery: praca w istniejącym zespole albo **własny zespół z pakietem sponsora założycielskiego** (PP-015).
- Rynek i negocjacje, projekt samochodu i R&D, drzewo technologii, finanse i sponsorzy.
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
- Kierunek wizualny najpierw jako 2–3 statyczne makiety do Twojego wyboru, potem właściwe ekrany: gęste tabele, ekran wyścigu, kronika.

## Faza 7+: Rozszerzenia
Serie juniorskie, Le Mans / WEC / GT (wizja endurance i zasady wejścia na wyścigi 24h z wcześniejszych ustaleń), tryb proceduralny od zera, wyzwania, edytor bazy, paczka fikcyjna, wydanie.

---

## Otwarte pytania

1. **Wiedza o przyszłości:** gracz wie, że Senna będzie wielki. Czy to celowa część zabawy („znam historię, więc łowię talenty”), czy ukrywamy prawdziwych juniorów (np. opcja „anonimowi juniorzy do debiutu”)? AI na pewno nie zna przyszłości.
2. **Serie w świecie 1950:** tylko mistrzostwa świata F1, czy też wyścigi F1 poza mistrzostwami? (Było ich wtedy więcej niż rund mistrzostw i dawały pieniądze startowe).
3. **Własne silniki:** czy gracz może zostać producentem silników (droga Ferrari, BRM, Hondy), czy tylko wybiera dostawcę?
4. **Prezentacja wyścigu w pierwszej wersji:** relacja tekstowa i tabela na żywo wystarczą, czy od razu chcesz mapę toru 2D?
5. **Język gry:** polski, angielski czy oba (i-18n od początku kosztuje niewiele)?
6. **Repozytorium na GitHubie:** prywatne czy publiczne (Peloton i Ping-Pong są publiczne)?
