# Paddock Principal — ROADMAP

**Status:** DRAFT (2026-09-25). Opiera się na [VISION.md](VISION.md) i [DECISIONS.md](DECISIONS.md).
**Zasada:** każda faza kończy się bramką, czyli czymś, co da się uruchomić i ocenić. Nie ma faz „tylko dokumentacja” poza fazą 0.

---

## Faza 0: Fundament dokumentacji
- [x] VISION, DECISIONS, ROADMAP
- [ ] Odpowiedzi na [otwarte pytania](#otwarte-pytania)
- [ ] Przepisanie istniejących dokumentów pod model ciągłego świata (lista niżej)
- [ ] `git init` + repo na GitHubie (zgodnie z Peloton D-029: historia gita to pamięć projektu)

**Bramka:** komplet dokumentów bez wewnętrznych sprzeczności.

## Faza 1: Pipeline danych historycznych (test wykonalności)
Najbardziej ryzykowna część całego pomysłu, więc robimy ją pierwszą.
- Import F1 1950–2025 z Jolpica-F1 (API zgodne z Ergastem): kierowcy, konstruktorzy, tory, wyniki, kwalifikacje.
- Model ocen: porównania z partnerem z zespołu i efekt „konstruktor × sezon”, co daje tempo kierowcy na sezon, krzywą kariery i sufit talentu.
- Plik jawnych nadpisań dla przypadków brzegowych (Indianapolis 500 w latach 1950–60, kierowcy jednego wyścigu, dzielone samochody).
- Raport sprawdzający: czy Fangio, Clark, Stewart, Lauda, Prost, Senna, Schumacher i Hamilton lądują tam, gdzie powinni? Gdzie model się myli i dlaczego?
- Szkielet danych personelu (Team Principal, projektant, projektant silnika): wstępnie ręcznie, dla czołowych zespołów każdej dekady.

**Bramka:** tabela ocen, którą przejrzysz i powiesz „to ma sens”. Jeśli się nie da, zmieniamy podejście, zanim powstanie reszta gry.

## Faza 2: Rdzeń świata
- Solucja .NET 9: przeniesienie z Pelotona determinizmu, strumieni RNG, zapisu SQLite, migracji, stabilnych ID i kalendarza.
- Encje: osoba (kierowca, personel, menedżer), organizacja, samochód i projekt, tor i jego wersje, seria, sezon, kontrakt.
- Oś czasu epok: przepisy, punktacja, bezpieczeństwo, ekonomia, technologie.
- Harmonogram ludzi: kiedy prawdziwa osoba pojawia się w świecie i w jakim stanie.
- SimRunner: przebieg N sezonów bez UI.

**Bramka:** SimRunner przechodzi 1950→2026 bez wyścigów (sama populacja): ludzie pojawiają się, starzeją i odchodzą, a zapis pozostaje mały.

## Faza 3: Silnik wyścigu dla wielu epok
- Model okrążeń sparametryzowany epoką: awaryjność, tankowanie, opony, aero, brudne powietrze, zmiana kierowcy w trakcie wyścigu.
- Strategia sztabu, pogoda, incydenty, kontuzje (PP-006).
- Race Spy od pierwszego dnia.
- **Test wierności historii (PP-012)** dla lat 1950–1960, a potem dla kolejnych dekad.

**Bramka (grywalności):** obejrzysz tekstową relację wyścigu z 1955 i z 1988 i powiesz, że czuć różnicę epok, a wyniki są wiarygodne.

## Faza 4: Pętla kariery (pierwszy grywalny sezon, jeszcze w CLI)
- Rynek kierowców i personelu, negocjacje (wzorzec z Pelotona).
- Projekt samochodu, R&D, drzewo technologii (PP-009).
- Finanse, sponsorzy, nagrody startowe (starting money w latach 50.).
- AI principali: archetypy, strategia przypisana do osoby, zwolnienia (wzorzec z Ping-Ponga).
- Powstawanie, upadki i wykupy zespołów oraz warunkowe zdarzenia historyczne (PP-004).

**Bramka:** jeden pełny sezon 1955 jest grywalny od A do Z, a decyzje mają odczuwalne konsekwencje.

## Faza 5: Wyróżnik, czyli żywa historia
- Kronika rozbieżności: Twoja oś czasu obok prawdziwej.
- Hall of Fame, rekordy, kompaktowa historia na 76+ lat (wzorzec z Ping-Ponga).
- Skrzynka i zdarzenia życiowe (0–3 na turę, cisza jest OK).
- Przejście od prawdziwych ludzi do generatora po 2026.

## Faza 6: UI w Godot
- Klient Godot 4.7 C# nad tymi samymi zapytaniami (wzorzec klienta z Pelotona).
- Gęste tabele (za wizją Ping-Ponga: arkusz, który wygląda dobrze), ekran wyścigu, kronika.

## Faza 7+: Rozszerzenia świata
- Serie juniorskie (F2/F3 i ich historyczne odpowiedniki) jako ścieżka rozwoju.
- Le Mans / WEC / IMSA / GT: wizja endurance (drabina GT4→GT3→LMP2→Hypercar, zespoły z kilkoma programami) i specyfikacja wejść na wyścigi 24h z wcześniejszych ustaleń; do spisania jako osobny dokument przed tą fazą.
- Tryb proceduralny od zera, tryb wyzwań, edytor bazy, paczka fikcyjna.

---

## Istniejące dokumenty: co z nimi

| Dokument | Decyzja |
|---|---|
| ARCHITECTURE.md | zostaje w dużej części; dopisać Godot, oś czasu epok i harmonogram ludzi |
| DETERMINISM_AND_EVENT_CONTRACTS.md | zostaje; dopisać strumienie `PeopleSchedule` i `HistoricalProposals` |
| PADDOCK_SPY_AND_DECISION_TRACING.md | zostaje; porównać z WORLD_SPY z Pelotona i ujednolicić |
| AI_PRINCIPAL_SYSTEM.md | zostaje jako baza; strategia przypisana do osoby, zespoły z kolejnych epok zamiast Red Bulla i Haasa |
| DATA_MODEL.md | **przepisać**: osoba zamiast kierowcy, organizacje z datami, projekt samochodu dla wielu epok, skala 1–20 w UI |
| CONTENT_FORMAT.md | **przepisać**: jedna baza świata zamiast paczek per sezon, zdarzenia warunkowe, nadpisania ocen |
| RACE_ENGINE_DESIGN.md | **przepisać**: model parametryzowany epoką, bez założeń współczesnego F1 |
| SAVE_FORMAT.md | zostaje w dużej części; dodać tabelę kroniki rozbieżności i Hall of Fame |
| README.md / DOCS_INDEX.md | zaktualizować po przepisaniu reszty |

---

## Otwarte pytania

1. **Własny zespół:** czy gracz może założyć swój zespół (jak Brabham, McLaren czy Williams), czy tylko dostaje pracę w istniejącym?
2. **Wiedza o przyszłości:** gracz wie, że Senna będzie wielki. Czy to celowa część zabawy („znam historię, więc łowię talenty”), czy ukrywamy tożsamość i potencjał prawdziwych juniorów? AI na pewno nie zna przyszłości.
3. **Serie w świecie 1950:** tylko mistrzostwa świata F1, czy też wyścigi F1 poza mistrzostwami i F2 (w latach 1952–53 mistrzostwa rozgrywano według przepisów F2)? Juniorzy jako abstrakcyjna pula czy prawdziwe serie?
4. **Gracz jako producent silników:** czy możliwa jest ścieżka „buduję własne silniki”, czy tylko wybór dostawcy (Coventry-Climax, Cosworth DFV, …)?
5. **Postęp czasu:** dzień po dniu jak w Pelotonie, czy od zdarzenia do zdarzenia z jednym przyciskiem „Dalej”?
6. **Prezentacja wyścigu w pierwszej wersji:** tekstowa relacja i tabela, czy od razu mapa toru 2D?
7. **Język gry:** polski, angielski czy oba?
