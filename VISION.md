# Paddock Principal — VISION

**Status:** ACCEPTED (owner, 2026-09-25)
**Rola:** kierunek projektu i przyjęte decyzje. Czytaj przed każdym innym dokumentem. Jeśli inny dokument mu przeczy, ten dokument wygrywa, a tamten idzie do poprawki.

## Jedno zdanie

> **Paddock Principal to manager motorsportu, w którym możesz zacząć w 1950 roku, prowadzić karierę przez całą historię wyścigów z prawdziwymi ludźmi i zobaczyć, jak Twoje decyzje zmieniają tę historię. Po 2026 świat żyje dalej bez końca.**

## Fantazja gracza

Jest 1955. Jesteś młodym menedżerem w małym brytyjskim zespole. Stirling Moss jeszcze nie wie, dla kogo będzie jeździł w przyszłym roku. Za trzy lata ktoś wpadnie na pomysł, żeby wstawić silnik za kierowcą. Może to będziesz Ty.

Trzydzieści lat później pewien Brazylijczyk wygrywa dla Ciebie mistrzostwo w zespole, który w prawdziwej historii nigdy nie istniał. Gra pokazuje Ci obie osie czasu obok siebie: prawdziwą i Twoją.

## Filary (w kolejności ważności)

### 1. Symulacja tworzy wynik
Historia nie jest skryptem. Prawdziwe wydarzenia są punktem startowym i punktem odniesienia, a nie wyrokiem. Nikt nie wygrywa, bo „tak było naprawdę”. Wygrywa, bo w tym świecie miał najlepszy samochód, talent i ludzi.

### 2. Historia, którą da się zmienić, i gra, która to pokazuje
Wyróżnik gry to nie prawdziwe nazwiska, tylko **rozbieżność**. Kronika rozbieżności porównuje Twoją oś czasu z prawdziwą: tytuły, zespoły, które nie powstały, technologie wymyślone wcześniej, kariery, które potoczyły się inaczej.

### 3. Jeden świat, jeden silnik
Tryb historyczny i proceduralny to ten sam świat. Różnią się tylko **źródłem ludzi** (harmonogram prawdziwych osób albo generator) i **osią czasu epok** (przepisy, technologie, ekonomia). Po wyczerpaniu prawdziwych danych generator płynnie przejmuje pałeczkę, więc kariera jest nieskończona.

### 4. Epoki są prawdziwe
Rok 1952 gra się inaczej niż 2012. Awaryjność, bezpieczeństwo, pieniądze, liczba wyścigów, punktacja, tankowanie, opony i aero wynikają z danych epoki, a nie z jednego współczesnego modelu F1.

### 5. Symetryczny świat i niepewna wiedza
AI i gracz używają tych samych mechanik. AI nie widzi ukrytych atrybutów. Prawda należy do symulacji, a wiedza do organizacji (za Peloton D-003/D-010).

### 6. Zarządzasz ludźmi, nie bolidem
Nie prowadzisz samochodu. Zatrudniasz kierowców, projektantów, inżynierów i mechaników, ustalasz priorytety i ryzyko. W wyścigu decyduje sztab, a jakość jego decyzji zależy od ludzi, których wybrałeś.

### 7. Wyjaśnialność
Każda ważna decyzja AI i każdy ważny wynik da się wyjaśnić: STATE / WHY / FORECAST dla gracza, Paddock Spy dla dewelopera.

### 8. Otwarte dane
Silnik działa również na fikcyjnych danych. Prawdziwe nazwiska, zespoły i tory to paczka danych, którą można podmienić. Edytor bazy powstaje w późniejszej fazie, a projekt przewiduje go od początku.

## Tryby gry (cel)

| Tryb | Start | Ludzie | Przepisy i technologie |
|---|---|---|---|
| **Historyczny** (flagowy) | dowolny rok od 1950 | prawdziwi z harmonogramu, po 2026 generator | historyczna oś czasu epok |
| **Proceduralny** | wybrany rok lub „dziś” | generator | historyczna lub proceduralna oś czasu |
| **Wyzwania** (później) | scenariusz | jak wyżej | jak wyżej |

## Konkurencja i pozycjonowanie

Najbliższy konkurent to **Team Principal: A Racing Manager** (Steam, wczesny dostęp od 02.2026): głęboki menedżer w stylu Grand Prix World, w którym da się edytować wszystko, a epoki są migawkami sezonów robionymi przez modderów. Nie ścigamy się z nim na „więcej funkcji” ani na „edytuj wszystko”. Wyróżniamy się trzema rzeczami, w tej kolejności:

1. **Najlepszy UI w gatunku.** Czytelny, gęsty, piękny; każda blokada i każda decyzja wyjaśniona (STATE / WHY / FORECAST). Ich największa słabość to brak onboardingu i niewyjaśnione blokady.
2. **Ciągła historia jako rdzeń gry, a nie mod:** ludzie według harmonogramu, epoki, technologie, propozycje historyczne, kronika rozbieżności.
3. **Polski i angielski.**

## Czego NIE robimy

- Nie wymuszamy prawdziwych transferów ani wyników.
- Nie odtwarzamy prawdziwych tragedii skryptem. Śmiertelność to opcja, domyślnie wyłączona.
- Nie dodajemy rubber-bandingu ani ukrytych bonusów dla AI.
- Nie piszemy liczb „na oko” jako faktów. Każda liczba balansu jest oznaczona jako szacunek, dopóki nie przejdzie kalibracji.

---

## Decyzje

Zmiana decyzji to nowy wpis, a nie cicha edycja starego. Oznaczenia `D-xxx` odnoszą się do DECISIONS.md w Peloton Managerze.

### Przyjęte 2026-09-25

**PP-001: Flagowy tryb to historyczne F1 od 1950.** Pierwsza grywalna wersja to kariera od dowolnego roku 1950+ z prawdziwymi ludźmi. Endurance, GT i serie juniorskie powstają później na tym samym silniku.

**PP-002: Stack: C#/.NET 9 (rdzeń headless) + UI w HTML/CSS/TypeScript (Svelte) w oknie Photino/WebView2, zapis w SQLite.** Godot odrzucony: UI to w 90% tabele, a AI piszące sceny Godota pracuje na ślepo (uzasadnienie w TECH §1.1). Infrastrukturę przenosimy z Peloton Managera. Stary kod w Pythonie nie istnieje.

**PP-003: Jeden ciągły świat zamiast paczek-migawek sezonów.** Baza świata zawiera osoby (z datami urodzenia i debiutu), organizacje (z datami powstania i końca), tory (z wersjami układu) oraz oś czasu epok. Rok startu to tylko miejsce, od którego zaczyna się symulacja.

**PP-004: Historia to punkt odniesienia, nie skrypt** (odpowiednik Peloton D-001). Prawdziwe zdarzenia (powstanie zespołu, transfer, wejście producenta) są **warunkowymi propozycjami** z oceną sensowności w bieżącym świecie. Tryb „Strict Historical” z wymuszanymi transferami jest usunięty.

**PP-005: Los prawdziwych kierowców wybiera się przy tworzeniu kariery.**
- *Potencjał*: prawdziwa kariera wyznacza sufit talentu (z niepewnością), a rozwój jest symulowany.
- *Trajektoria*: umiejętności rok po roku podążają za prawdziwą karierą.
- Do tego suwak losowości.
Oba warianty korzystają z tego samego wyliczonego profilu kierowcy.

**PP-006: Śmiertelne wypadki są opcją, domyślnie wyłączoną.** Domyślnie poważne wypadki kończą się kontuzją albo końcem kariery, z ryzykiem zależnym od bezpieczeństwa epoki. Prawdziwe tragedie nigdy nie są skryptowane.

**PP-007: Gracz to menedżer (osoba), a nie zespół** (odpowiednik Peloton D-004/D-005). Może zmieniać pracodawców i zostać zwolniony, a kariera trwa dalej.

**PP-008: Epoki są danymi.** Przepisy sportowe i techniczne, punktacja (w tym zasada N najlepszych wyników i dzielenie punktów przy zmianie kierowcy), bezpieczeństwo, awaryjność i ekonomia to wpisy osi czasu, a nie stałe w kodzie.

**PP-009: Technologie to drzewo z historycznymi datami jako punktem odniesienia dla AI.** Gracz (i AI) może wprowadzić technologię wcześniej albo później niż w rzeczywistości. Przykłady: silnik centralny, efekt przyziemny, turbo, monokok z włókna węglowego, półautomatyczna skrzynia.

**PP-010: ~~Pieniądz to indeks epoki bez automatycznej inflacji.~~** Zastąpione przez PP-025.

**PP-011: Oceny kierowców historycznych są wyliczane, a nie wpisywane ręcznie.** Pipeline danych wyprowadza je z wyników (porównania z partnerem z zespołu z odjęciem efektu samochodu w danym sezonie). Ręczne korekty to jawny, opisany plik nadpisań.

**PP-012: Test wierności historii to bramka jakości.** Symulacja okresu (np. 1950–1960) bez gracza musi dawać wiarygodne rezultaty w stosunku do prawdziwych: dominujący kierowcy są w czołówce, a nie w loterii. Nie oczekujemy identycznych wyników.

**PP-013: Skala: atrybuty 1–20 plus ogólna ocena 1–100, zawsze liczby całkowite.** Ułamki nigdy nie są pokazywane. Wewnętrzne wartości są ciągłe, a drobne postępy pokazują strzałki trendu. Scouting pokazuje pasma (np. 16–19).

**PP-014: Prawdziwe dane to wymienna paczka.** Silnik musi działać na paczce fikcyjnej. Licencje na nazwy są problemem przyszłej publikacji, a nie architektury.

### Przejęte z Peloton Managera bez zmian

D-006 (postęp w dniach, sterowany zdarzeniami), D-007 (stabilne ID nigdy nieużywane ponownie), D-010 (AI bez wszechwiedzy), D-013 (zakres determinizmu), D-014 (prognozy nie zmieniają stanu i nie zużywają RNG), D-015 (kompaktowanie historii nie zmienia przyszłości), D-023–D-027 (Spy jako obowiązkowa infrastruktura, prawda debugowa nigdy nie staje się wiedzą w grze).

### Przyjęte 2026-09-25 (druga runda)

**PP-015: Własny zespół.** Na starcie kariery gracz może założyć własny zespół: ustala budżet i dostawcę silników, a sponsor założycielski stawia warunki z terminami (np. kierowca danej narodowości, talent z danej serii, podium w ciągu 3–5 lat). W trakcie kariery zespół można założyć od zera (realnie głównie do lat 90., bo bariery wejścia rosną z epoką) albo wykupić upadający (w każdej epoce). Szczegóły w DESIGN §3.

**PP-016: Czas płynie dzień po dniu.** „Dalej” przewija dni do najbliższej sprawy wymagającej uwagi.

**PP-017: Mało dokumentacji.** Tylko README, VISION (z decyzjami), ROADMAP, DESIGN, TECH, plus krótki AGENTS.md dla agentów AI pomagających w repo. Nowy dokument powstaje wyłącznie dla dużego, osobnego systemu, i to tuż przed jego budową. Kod i historia gita są dokumentacją szczegółów.

### Przyjęte 2026-09-25 (trzecia runda)

**PP-018: Wiedza o przyszłości jest częścią zabawy, ale świat ma punkt wejścia.** Gracz może wykorzystywać znajomość historii. Jak mocno świat trzyma się historii, ustawia w konfiguracji kariery (presety plus suwak „siła historii”). Ludzie wchodzą do świata w punkcie wejścia, czyli na najniższym modelowanym szczeblu drabinki. Na start modelujemy tylko mistrzostwa świata F1, więc punktem wejścia jest abstrakcyjna pula talentów, uzupełniana fikcyjnymi kierowcami. Szczegóły w DESIGN §2.

**PP-019: Gracz może zostać producentem silników** i sprzedawać je zespołom klienckim.

**PP-020: Najpierw mocny backend, potem UI.** Do fazy 5 włącznie gramy w CLI. Mapa toru 2D powstaje po UI.

**PP-021: Dwa języki: polski i angielski.** Kod, identyfikatory i commity są po angielsku. Wszystkie teksty dla gracza idą przez klucze tłumaczeń od pierwszego dnia (także w CLI). Dokumentacja projektu zostaje po polsku.

**PP-022: Repozytorium jest publiczne** (github.com/DatJikun/paddock-principal), a pomagają w nim również agenci AI znajomych. Zasady pracy dla nich są w AGENTS.md. Dane historyczne w repo muszą mieć zgodną licencję i atrybucję (TECH §6.1).

### Przyjęte 2026-09-25 (czwarta runda)

**PP-023: UI to główny wyróżnik, więc nie zostawiamy go na koniec** (uzupełnia PP-020). Grywalne ekrany nadal powstają po backendzie, ale **język wizualny** (makiety, typografia, kolory, komponenty tabel) tworzymy równolegle od fazy 1. Backend od początku zwraca to, czego UI potrzebuje: stan, powody i prognozy, a nie tylko liczby.

**PP-024: Podział pracy.**
- **Claude:** decyzje, architektura, specyfikacje zadań, review, UI.
- **Grok 4.7 (Cursor):** implementuje dobrze opisane zadania backendowe.

Zadania są opisane jako GitHub Issues z kryteriami akceptacji. Implementacja idzie na gałęzi, jako PR, po review Claude'a, a merge robi właściciel albo Claude po review. Zasady dla kodera są w AGENTS.md.

### Przyjęte 2026-09-25 (piąta runda)

**PP-025: Pieniądze rosną z popularności sportu** (zastępuje PP-010). Model popularności (globalny i per kraj; wyrównana walka o tytuł i krajowi bohaterowie podnoszą, dominacja obniża) steruje pulą TV i nagród, rynkiem sponsorów i naturalnym wzrostem pensji. W trybie historycznym bazą jest oś czasu epok. Szczegóły w DESIGN §9.

**PP-026: Infrastruktura jest nieskończona.** Jakość obiektów liczy się względem ruchomej granicy technologii, nowe rodzaje obiektów przychodzą z epokami, a regulamin resetuje przewagi. Nie ma endgame'u. Szczegóły w DESIGN §4.3.

**PP-027: Budowanie auta to kluczowy system z realnymi kompromisami.** Są trzy drogi pozyskania (samochód kliencki, własna konstrukcja, umowa z producentem aż po status zespołu fabrycznego), a koncepcja składa się z osi, na których każdy biegun ma swoją cenę. Szczegóły w DESIGN §5.

### Przyjęte 2026-09-25 (szósta runda, po researchu Team Principal)

**PP-028: Własność i udziały: tak. Szpiegostwo: nie.** Zespoły mogą sprzedawać i kupować udziały, a zespoły B są dozwolone. Limity regulaminowe i rynkowe nie pozwalają przejąć całej F1; w piaskownicy można je wyłączyć. Nie ma osobnego systemu szpiegostwa: kopiowanie rozwiązań rywali wynika z umiejętności inżynierów i z przepływu ludzi między zespołami.

**PP-029: Delegowanie zamiast mikrozarządzania, ale gra jest modularna.** Nie ma magazynu części; wyścigiem steruje zatrudniony strateg. Ręczna kontrola (np. pit-stopy) to opcjonalne moduły włączane w ustawieniach kariery. Każdy system musi dać się zaprojektować tak, żeby mógł być delegowany albo sterowany ręcznie.

**PP-030: Umowy z dostawcami to ważny system.** Opony, paliwo, części i silniki; umowy fabryczne, partnerskie i klienckie; umowy wieloletnie; zespół numer 1 współtworzy technologię z dostawcą. Każdy dostawca ma swoje plusy i minusy. Szczegóły w DESIGN §5.4.

### Przyjęte 2026-09-25 (siódma runda)

**PP-031: Opcjonalny tryb bez liczb.** Atrybuty, oceny i osiągi auta poznajesz wyłącznie z opinii swoich ludzi. Ich trafność zależy od jakości personelu. Technicznie to tylko inna prezentacja wiedzy zespołu (INV-003), więc każdy system od początku zwraca wiedzę z niepewnością, a nie gołe liczby. Szczegóły w DESIGN §13. Do tego pełny profil kierowcy z trzema warstwami dopasowania do torów: atrybuty, znajomość toru i ukryte powinowactwo (w trybie historycznym wyliczone z prawdziwych wyników); szczegóły w DESIGN §6.1.

**PP-032: Rozwój auta po krzywej S w każdym cyklu regulaminowym.** Zasoby dzielisz na trzy strumienie: bieżące auto, nową koncepcję w tym sezonie („wersja B”) i auto na przyszły sezon. Szczegóły w DESIGN §5.3.

**PP-033: Personel to kluczowi ludzie plus działy z liczebnością.** Wydajność działów ma malejące korzyści, a talenty mogą wyrosnąć wewnątrz działów. Szczegóły w DESIGN §6.2.

**PP-034: Wspólny katalog zasad dla trybu historycznego i proceduralnego,** z polityką regulaminową (propozycje i głosowania) tam, gdzie epoka ją przewiduje. Research zasad robi Grok.

