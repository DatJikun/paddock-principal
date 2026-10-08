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

### 2. Historia, którą da się zmienić
Wyróżnik gry to nie prawdziwe nazwiska, tylko to, że **od pierwszego dnia historia toczy się po swojemu**. Gra nie porównuje Twojej osi czasu z prawdziwą i nie ocenia, czy świat jest „zgodny z historią” (PP-062). Gracz zna przeszłość sam.

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
2. **Ciągła historia jako rdzeń gry, a nie mod:** ludzie według harmonogramu, epoki, technologie, propozycje historyczne.
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

**PP-002: Stack: C#/.NET 9 (→ .NET 10, PP-036) (rdzeń headless) + UI w HTML/CSS/TypeScript (Svelte) w oknie Photino/WebView2, zapis w SQLite.** Godot odrzucony: UI to w 90% tabele, a AI piszące sceny Godota pracuje na ślepo (uzasadnienie w TECH §1.1). Infrastrukturę przenosimy z Peloton Managera. Stary kod w Pythonie nie istnieje.

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

**PP-009: ~~Technologie to drzewo z historycznymi datami jako punktem odniesienia dla AI.~~** Zastąpione przez PP-042. Gracz (i AI) może wprowadzić technologię wcześniej albo później niż w rzeczywistości. Przykłady: silnik centralny, efekt przyziemny, turbo, monokok z włókna węglowego, półautomatyczna skrzynia.

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

**PP-032: ~~Rozwój auta po krzywej S.~~** Zmienione w rundzie ósmej: rozwój zależy od zapasu własnej koncepcji (brak wspólnej krzywej), do tego konto rozwoju i elastyczny moment wdrożenia nowej koncepcji. Szczegóły w DESIGN §5.3.

**PP-033: Personel to kluczowi ludzie plus działy z liczebnością.** Wydajność działów ma malejące korzyści, a talenty mogą wyrosnąć wewnątrz działów. Szczegóły w DESIGN §6.2.

**PP-034: Wspólny katalog zasad dla trybu historycznego i proceduralnego,** z polityką regulaminową (propozycje i głosowania) tam, gdzie epoka ją przewiduje. Research zasad robi Grok.

### Przyjęte 2026-09-26 (ósma runda)

**PP-035: Profil kierowcy i kontrakty.**
- Atrybuty: zakręty, hamowanie, płynność (waga zależna od epoki), wyprzedzanie, obrona, regularność, opanowanie, adaptacja, deszcz, kondycja, informacja zwrotna.
- Doświadczenie to liczniki z konkretnym działaniem, a nie atrybut.
- Preferencje: balans, trakcja, styl hamowania.
- Bez zmęczenia w widoku składu.
- Tryb porównania karier, sezonów i pojedynków bezpośrednich.
- Brak „wartości rynkowej”. Są za to: kontrakt, klauzule (głęboki system), zaufanie i obietnice z terminami i warunkami.

Szczegóły w DESIGN §6.1, §6.3 i §10.

**PP-036: .NET 10 (LTS) zamiast .NET 9.** Na maszynie jest SDK 10, a .NET 10 ma długie wsparcie.


**PP-037: Na razie bez szarej strefy i oszustw.** Nie ma wykorzystywania luk w przepisach, protestów rywali ani zakazanych systemów z ryzykiem wykrycia. Temat można otworzyć później jako opcjonalny moduł (PP-029).

### Przyjęte 2026-09-26 (dziewiąta runda: UI)

**PP-038: Kierunek UI to C („barwy epoki”), z 1976 jako wzorcem jakości.**
- Skórka ery zmienia się z dekadą albo jest ustawiana na stałe (opcja).
- Kolory interfejsu pochodzą z barw zespołu, a opcjonalnie ze sponsora tytularnego.
- Kierunek A („ściana boksu”) odrzucony jako AI-slop; wariant 1992 odrzucony za zbyt szerokie kroje.
- Pulpit jest zbudowany wokół „Do zrobienia”, a pozycja auta względem stawki jest pokazana paskami od zielonego do czerwonego.
- Zasady w DESIGN §14.2. **Gra ma być fantastyczna albo jej nie wydajemy.**

**PP-039: Paddock Monthly, czyli interaktywny miesięcznik ze świata gry.** Rynek i plotki, talenty, wyniki innych serii, technika, pieniądze. Wszystko prowadzi do aktualnych danych (klasyfikacje, profile, kontrakty). Szczegóły w DESIGN §14.3.

**PP-040: Ocena ogólna jako gwiazdki (0–5, z połówkami) zamiast liczby 1–100** (zmienia PP-013).
- Atrybuty zostają w skali 1–20.
- Gwiazdki pochodzą z wiedzy zespołu, a nie z prawdy symulacji: przy obcych kierowcach to pasmo (np. 3–4★), a potencjał pokazują gwiazdki „duchy”.
- Rynek filtruje i sortuje po gwiazdkach, tak jak w Ping-Pong Managerze.

**PP-041: Dane historyczne z Jolpica-F1 (CC BY-NC-SA 4.0) nie trafiają do repo.** Pipeline jest w repo, a bazę każdy buduje lokalnie. Wydanie komercyjne wymaga własnej, niezależnie zebranej bazy faktów albo zgody właściciela danych. Decyzja przed fazą wydania (ROADMAP, otwarte pytania).

**PP-042: Przełomy technologiczne od ludzi zamiast drzewka** (zastępuje PP-009).
- Nie ma drzewka i nie da się niczego „zrushować” pieniędzmi.
- Pomysły zgłaszają inżynierowie. Ich atrybut innowacyjność decyduje o częstotliwości i średniej jakości pomysłów, ale rozkład ma długi ogon: słaby inżynier może rzadko trafić coś genialnego.
- Wykonanie zależy od precyzji, działu i infrastruktury, a pomysł może nie wypalić.
- Obowiązuje okno gotowości epoki (można wyprzedzić historię o kilka lat, nie o dekadę). Rywale kopiują, FIA może zakazać.

Szczegóły w DESIGN §4.2.

### Przyjęte 2026-09-26 (dziesiąta runda)

**PP-043: Rozwój auta ma dwie ścieżki, a MVP zaczyna od autonomicznych inżynierów** (rozwija PP-029 i PP-032).
- **Ścieżka A, autonomiczna (MVP):** gracz ustala podział zasobów (bieżące auto / konto rozwoju / przyszły rok, opcjonalnie priorytety obszarów). Konkretne projekty wybierają inżynierowie według swoich atrybutów, doświadczenia, stażu i adaptacji w zespole. Gdy gracz zmienia podział, inżynierowie mogą odpowiedzieć w skrzynce („jeszcze 2 tygodnie, jesteśmy blisko przełomu”). Gracz trzyma się planu albo tnie projekt.
- **Ścieżka B, ręczna (później):** gracz sam wybiera projekty. To opcjonalny moduł włączany w ustawieniach kariery.
- **Technicznie to ta sama logika co u AI:** zespoły AI i tak potrzebują inżynierów, którzy sami wybierają projekty. Ścieżka A to ta logika zastosowana do zespołu gracza, więc MVP nie wymaga osobnego systemu. Ścieżka B to tylko inne źródło decyzji o projekcie.

Szczegóły w DESIGN §5.3.

**PP-044: Szef zespołu ma atrybuty** (potwierdza DESIGN §6.2). Dotyczy gracza i szefów AI: negocjacje, zarządzanie ludźmi, polityka, biznes. Czy atrybuty ma też właściciel albo prezes zespołu (zarząd, §15), jest nadal otwarte (ROADMAP, otwarte pytania).

### Przyjęte 2026-10-03 (jedenasta runda)

**PP-045: Multiplayer online dla znajomych.**
- Kilku ludzi gra w jednym świecie, a każdy prowadzi własny zespół. Wspólnego zespołu z podziałem ról na razie nie ma.
- **Wspólna data:** czas rusza dopiero wtedy, gdy wszyscy klikną „Dalej”. Pulpit działa jak w grze dla jednego gracza, a sprawa wymagająca decyzji (np. negocjacje) zatrzymuje czas dla wszystkich.
- **Wyścig oglądany razem na żywo.** Wszyscy widzą ten sam przebieg w tym samym tempie, a każdy wydaje polecenia swojemu zespołowi.
- **Model techniczny: host i goście.** Symulację liczy wyłącznie komputer hosta. Goście wysyłają tylko swoje decyzje i dostają z powrotem stan świata widziany przez swój zespół (prawda kontra wiedza, INV-004). Szczegóły w TECH §5.1.
- **Kolejność:** rdzeń od fazy 2 obsługuje wielu menedżerów, a sieć i rozgrywka online powstają razem z prawdziwym UI (faza 6). Multiplayer w samej konsoli nie ma sensu.

### Przyjęte 2026-10-03 (dwunasta runda)

**PP-046: Tryby gry to niezależne osie, a presety je składają** (rozwija DESIGN §2.3, nie zmienia PP-004, PP-005 ani PP-018).
- **Oś „Ludzie”:** (a) prawdziwa trajektoria, (b) prawdziwy potencjał (może się zmienić), (c) prawdziwe nazwiska z losowymi potencjałami i skillami, (d) w pełni generowani. Generator (strumień `People`) obsługuje (c) i (d) oraz świat po 2026.
- **Oś „Przepisy”:** (a) historyczne, (b) głosowane co sezon (jak w Motorsport Managerze: zespoły głosują zgodnie z własnym interesem, DESIGN §4.1). Proceduralna oś czasu to tylko ta druga opcja.
- **Oś „Zachowanie AI”:** (a) odtwarza historię, czyli prawdziwe transfery, wejścia i wyjścia zespołów, i rozjeżdża się dopiero po ingerencji gracza (siła historii 100%), (b) reaguje na bieżącą sytuację, co daje efekt motyla, (c) czysta losowość. AI nadal nie zna przyszłości (D-010); odtwarzanie historii to scenariusz zdarzeń, a nie wiedza.
- **Reszta:** suwaki losowości i śmiertelności, rok startu, zespół (DESIGN §2.3).
- **Presety:** *Najbardziej historyczny* = trajektoria + historyczne przepisy + AI odtwarza historię. *Zbalansowany* (domyślny, „normalny”) = prawdziwy potencjał + historyczne przepisy + AI reaguje. *Chaos* = dowolna kombinacja osi, np. prawdziwe nazwiska z losowymi umiejętnościami i głosowanymi przepisami, aż po w pełni generowany świat.
- **Skutek techniczny:** konfiguracja to jeden typ `CareerConfig` zapisywany w `meta`, a każdy system pyta o oś, a nie o nazwę trybu. Fazy 2 i 4 zaczynamy od tego typu.
- **Uwaga o fazach:** właściciel zdecydował o wcześniejszym zaczęciu wybranych zadań fazy 2 (domena świata, tick dnia, kolejka komend, SimRunner), równolegle z domykaniem fazy 1.

### Przyjęte 2026-10-04 (dwunasta runda: oceny kierowców)

**PP-047: Oceny względem własnej epoki, wspólny łuk kariery, gwiazdki z atrybutów** (doprecyzowuje PP-040).
- **Ocena względna:** prawdziwy kierowca jest oceniany na tle stawki swoich czasów, a nie kierowców z innych dekad. Najlepsi każdej epoki (Fangio, Clark, Fittipaldi, Hamilton) są na szczycie skali, a dominacja nad rywalami podnosi ocenę.
- **Gwiazdki są dynamiczne:** to średnia atrybutów 1–20 podzielona przez 4 (20/20 = 5★, 10/20 = 2,5★). Nie ma sztywnych progów rankingowych.
- **Łuk kariery jest taki sam dla wszystkich:** rozwój, stabilizacja, szczyt, a spadek dopiero około 36–40 lat. Dane decydują o tym, jak wysoko i kiedy kierowca dochodzi do szczytu. Kierowca, który odszedł na szczycie (Fangio), słabnie dopiero po swoim ostatnim prawdziwym sezonie.
- **Mało danych = ostrożniejsza ocena:** kierowca z małą liczbą porównań z partnerem z zespołu jest przyciągany do średniej (Castellotti).
- **Indywidualny charakter kierowcy wyrażają przede wszystkim cechy** (DESIGN §6.1): dają bonusy i minusy i mogą też zostać nabyte (albo utracone) w trakcie kariery.
- Liczby (skala poziomów, siła przyciągania, wiek spadku) są szacunkami do dalszej kalibracji przez właściciela, który może nanosić ręczne korekty.

### Przyjęte 2026-10-04 (trzynasta runda: przegląd Team Principal)

**PP-048: Wnioski z przeglądu gry Team Principal (konkurencja).**
- **Strata do lidera rozbita na składniki, zawsze dla konkretnego toru.** Gra pokazuje, ile czasu na okrążeniu daje osobno auto, silnik i reszta pakietu, ale tylko w odniesieniu do wybranego toru (np. najbliższego wyścigu). Jednej uniwersalnej liczby „na sezon” nie ma, bo zależy ona od charakterystyki toru.
- **Tory opisane matematycznie.** Układ toru to geometria (linia środkowa, zakręty, proste), z której gra wylicza charakterystykę (udział wolnych, średnich i szybkich zakrętów oraz prostych) i rysuje mapkę. Dzięki temu tory są spójne, dają się dostosowywać, a nowe da się tworzyć bez rysowania ręcznie. Profil auta według typów zakrętów wynika z tej samej geometrii.
- **Kredyty z akceptacją zarządu:** kilka ofert (kwota, oprocentowanie, okres), ograniczona liczba naraz, zgoda zarządu wymagana.
- **Relacje w liczbach:** profil kierowcy i personelu pokazuje krótką listę najlepszych i najgorszych relacji z zespołami i ludźmi (top 3 / bottom 3).
- **Liczba prób w negocjacjach zależy od relacji:** wieloletni kierowca z wysoką lojalnością i morale daje dużo prób, obcy kierowca tylko 2–3.
- **Głosowanie nad przepisami w dwóch trybach do wyboru w konfiguracji kariery:** zwykły (każde głosowanie to jeden głos) albo z bankiem głosów (wstrzymanie się odkłada głos do prywatnego banku, który można później wydać na ważniejsze głosowanie).
- **Na później (po MVP):** specjalizacja projektantów (osobne oceny dla wolnych, średnich i szybkich zakrętów oraz oporu, nadająca autu charakter) oraz kierunek rozwoju jako dźwignia gracza (np. moc / niezawodność / efektywność, priorytet rozwoju umiejętności kierowcy).
- **Odrzucone:** kradzież technologii rywali (PP-037), fabryka i magazyn części (PP-029).

**PP-049: Miejsce w stawce wynika z parametrów auta; tory z geometrii potwierdzone prototypem** (zmienia pierwszy punkt PP-048).
- **Zamiast rozbicia straty na składniki** gra pokazuje przybliżone miejsce auta w stawce. Wynika ono samo z fizycznych parametrów auta (przyczepność mechaniczna, docisk rosnący z prędkością, prędkość maksymalna, przyspieszenie, hamowanie) zestawionych z geometrią toru. Na różnych torach różne parametry ważą inaczej, bez osobnych tabel wag.
- **Prototyp Monzy (2026-10-04):** 65 punktów kontrolnych zamkniętej krzywej odtwarza tor długości 5,793 km. Z zakrzywienia wychodzą prędkości w zakrętach (Rettifilo około 78 km/h, Roggia około 96 km/h), udział prostych (około 88%) i mapka. Na Monzie +10 km/h prędkości maksymalnej daje około 0,8 s na okrążeniu, a +10% docisku tylko około 0,2 s, czyli zgodnie z charakterem toru. Liczby są szacunkami do kalibracji.
- **Na później:** rywalizacje między kierowcami powstają z kolizji na torze, a przyjaźń i większy wzajemny szacunek na torze z wcześniejszych wspólnych startów w seriach juniorskich.

### Przyjęte 2026-10-04 (czternasta runda: zakres MVP)

**PP-050: Zakres MVP i odpowiedzi na pytania fazy 4.**
- **MVP = sezon 1955 w przejętym istniejącym zespole.** Własny zespół, sponsor założycielski, zakładanie i wykup zespołu w trakcie kariery są po MVP. Multiplayer jest razem z prawdziwym UI (faza 6), a rdzeń jest już pod niego pisany (PP-045).
- **Finanse:** szacunki zamiast danych z 1955 są w porządku. Saldo może spaść poniżej zera, a na odbicie się jest **cały sezon** (nie 90 dni); można też wziąć kredyt (PP-048). Popularność sportu jest globalna. Kwoty tylko w dolarach.
- **Sponsorzy:** trzy miejsca na sponsora; na każde miejsce co najmniej 3–5 sponsorów do wyboru. Warunek „talent z danego kraju” oznacza kierowcę tej narodowości.
- **Auta i stawka:** każdy zespół wystawia dokładnie **dwa auta i dwóch stałych kierowców**. Bez prywatnych zgłoszeń, sprzedaży podwozi i przesiadek między autami w trakcie sezonu (uproszczenie historii). Siła aut na starcie odtwarza historię (Mercedes mocny w 1955). AI widzi siłę aut tak jak prawdziwy szef zespołu (wyniki, tempo), ale nie zna przyszłości.
- **Balans kierowca–auto** wynika z modelu ocen: dla każdej epoki proporcja rozrzutu auta i rozrzutu kierowcy jest wyliczona z prawdziwych wyników, a silnik wyścigu używa tych proporcji.
- **Zarząd:** nowy szef ma ochronę co najmniej przez pierwszy sezon, dłużej przy wysokiej reputacji. Właściciele i zarządy na razie nie mają atrybutów. Założyciela własnego zespołu nie da się zwolnić. Zwolniony gracz ogląda dalej świat jako neutralny obserwator, dopóki nie znajdzie nowej pracy.
- **Przejęcie zespołu (po MVP):** przejmujesz wszystko dokładnie tak, jak jest: kontrakty, budżet, długi i zobowiązania. Nic nie jest generowane ani pomijane, bo gracz i AI działają według tych samych zasad.
- **Prawdziwe wydarzenia z historii zespołów** nie są pokazywane graczowi jako opisy. Świat gry jest wirtualny, a gracz może porównać go z prawdziwą historią sam.

**PP-051: Scenariusze startowe i wydarzenia oskryptowane (pomysł na po MVP).**
- **Własna baza startowa sezonu:** gracz wybiera sezon startowy (np. 2016) i zmienia w jego bazie, co chce: zespoły, kierowców, kontrakty, siłę aut. Na razie zmiany nanosi Claude na prośbę właściciela, później edytor bazy (faza 7+).
- **Wydarzenia oskryptowane:** scenariusz może zaplanować zdarzenia w czasie, np. start w 2012, a w 2013–2014 ktoś zauważa Verstappena dzięki wynikom w seriach juniorskich i trafia on do akademii Mercedesa. Wydarzenia działają na zwykłych zasadach świata, tak jak propozycje historyczne (PP-004).
- **Wiedza AI o historii jako opcja kariery:** to, czy AI odtwarza historię, czy tylko reaguje na sytuację, jest ustawieniem kariery (oś „zachowanie AI”, PP-046). Domyślnie AI ocenia siłę aut tak jak prawdziwy szef zespołu i nie zna przyszłości.


**PP-052: Wyścig na żywo: w MVP silnik okrążeniowy, ale z furtką na prawdziwą symulację ciągłą.**
- **MVP:** zostaje obecny silnik okrążeniowy (taśma zdarzeń, `RaceTape`). Mapa 2D z góry z kropkami (HANDOFF_UI, „Wyścig na żywo”) działa już w MVP, a pozycje aut na torze są z taśmy wyliczane (interpolacja po geometrii toru). Jest to przybliżenie i ma być jako takie oznaczone w kodzie.
- **Po MVP:** osobny silnik ciągły (pozycja i prędkość każdego auta w każdej chwili, wyprzedzanie, kolizje wynikające z symulacji, bez scenariuszy). Wymiana silnika nie może wymagać zmian w reszcie gry ani w UI.
- **Furtka (kontrakt):** (1) całą symulację wyścigu zamyka jeden interfejs (`IRaceSimulator`: wejście = stawka, tor, pogoda, przepisy epoki, ziarno; wyjście = `RaceTape`); (2) taśma ma poza zdarzeniami opcjonalne **klatki pozycji** (czas, auto, miejsce na torze, prędkość) i to z nich rysuje się mapa, niezależnie od silnika; (3) UI, klasyfikacja, zapis i kronika czytają wyłącznie taśmę, nigdy wnętrza silnika; (4) wybór silnika to ustawienie wyścigu (np. okrążeniowy dla wyścigów w tle, ciągły dla oglądanego), ale oba muszą spełniać ten sam kontrakt i niezmienniki TECH §3 (determinizm, osobny strumień RNG, prawda vs wiedza).
- **Wyścigi w tle** (inne serie, świat bez gracza) mogą zawsze używać szybkiego silnika okrążeniowego; test zgodności (statystyki wyników obu silników w tych samych warunkach mają być zbliżone) jest zadaniem po MVP.

### Przyjęte 2026-10-04 (piętnasta runda: rozwój auta, tory, pętla kariery)

**PP-053: Rozwój auta: wdrożenie gotowej koncepcji to decyzja gracza i wymaga czasu produkcji** (uzupełnia PP-043).
- **Decyzja zamiast zegara.** Gdy koncepcja jest gotowa, gracz (lub AI) widzi stan prac i decyduje: wdrażamy teraz, czy czekamy na dalsze zyski. Przetrzymanie gotowej koncepcji (nawet przez zmianę sezonu) jest dozwolone i **nie ma sztucznej kary**: traci się tylko względem rywali, którzy się rozwijają, oraz przez zanik konta rozwojowego. Starsze terminy („po N wyścigach”, „następny sezon”) działają dalej dla zgodności, ale nie są głównym przepływem i mają być wycofane po ustabilizowaniu AI.
- **Wdrożenie = produkcja.** Zatwierdzenie uruchamia produkcję na realistyczny czas (ESTIMATE: ok. 40 dni w 1955, ok. 190 w 2025, skalowane liczbą inżynierów epoki do czasu zatwierdzenia modelu działów z DESIGN §6.2). Koszt to połowa kosztu rozwoju koncepcji (ESTIMATE), płatna w całości w dniu zatwierdzenia. Stare auto jeździ w czasie produkcji, nowe wchodzi dzień po jej zakończeniu. W trakcie produkcji nie zmienia się terminu ani nie anuluje (koszt jest stracony).
- **Informacja do decyzji** tylko w granicach wiedzy (INV-003): przedział oczekiwanego dalszego zysku z trwających prac, dni do następnego wyścigu, czas i koszt produkcji. Gdy koncepcja staje się gotowa, trafia do skrzynki decyzja „wdrażamy czy rozwijamy dalej” (domyślnie po 14 dniach: rozwijamy dalej).
- **Zmianę sezonu 1 stycznia wykonuje host, nie moduł rozwoju.** Rozwój tylko reaguje na zdarzenie zmiany sezonu.
- **Po MVP:** przełomy technologiczne (PP-042), model działów inżynierskich (DESIGN §6.2), anulowanie produkcji. Zrównoważenie „czekać czy wdrażać” wymaga działających rywali AI i sprawdza je scenariusz bramkowy T48.

**PP-054: Tory z punktów kontrolnych: jedno źródło dla symulacji i UI.**
- **Jeden plik JSON na układ** (`data/authored/tracks/geometry/<layout_id>.json`: punkty kontrolne w metrach, `source`, `notes`) i zamknięta krzywa Catmulla-Roma liczona z tych punktów. Ten sam format czyta i zapisuje edytor torów. **Symulacja i UI czytają te same pliki**; zmiana pliku zmienia oba. Walidator pilnuje długości (zgodnej z `length_km`), braku samoprzecięć i zbyt ostrych zagięć.
- **Pokrycie:** wszystkie 26 układów używanych w latach 1950–60. Kolejne okresy kolejnymi porcjami.
- **Kształty są przybliżone** (ESTIMATE): narysowane z ogólnej wiedzy o układach i publicznych opisów tekstowych, bez obrysowywania cudzych map i bez danych osób trzecich (PP-041). Każdy plik opisuje w `source` i `notes` pewność kształtu. Poprawia się je w edytorze z własnych obrazów właściciela (obrazy nie trafiają do repo).
- **Znane ograniczenia:** nie da się zadeklarować celowego skrzyżowania (most, tunel), a Nürburgring 1976 rysuje się chwilowo z układu z 1951.

**PP-055: Założenia tymczasowe pętli kariery do czasu wyników wyścigów w pętli (T47).**
- **Zastępcza kwota startowa:** bez wpływów z wyścigów i sponsorów każdy zespół od razu bankrutowałby (zmierzone: kariera od 1950 bez kontraktów w 1959), więc każdy zespół dostaje co 1 stycznia tę samą kwotę (ESTIMATE: 30% typowego budżetu epoki). Wycofanie jest jednolinijkowe, gdy T47 zacznie księgować prawdziwe wyniki.
- **Cele zarządu bez tabeli wyników** są liczone jako niespełnione (zasada „nieznany fakt = niespełniony”), więc zaufanie zarządu spada, ale nikt nie jest zwalniany, bo przeglądy idą w dni wyścigowe. Po T47 stan liczy się od nowa.
- **Wygasanie pozycji w skrzynce jest aktywne** w przebiegu (wygasłe pozycje rozstrzygają się domyślną opcją jako zapisane komendy).
- **Poprawka błędu z T39:** kontrakt podpisany komendą nie ginie już na końcu poranka (księga kontraktów jest związana ze światem sesji), co zmienia świat każdego przebiegu względem wcześniejszej wersji.

### Przyjęte 2026-10-05 (szesnasta runda: dokumentacja)

**PP-056: Dokumentacja w HTML generowana z .md i przewodnik „Jak działa gra”** (rozszerza PP-017).
- **Pliki .md zostają jedynym źródłem.** Strona HTML powstaje z nich skryptem `node tools/docs/build-docs.mjs` (katalog `build/docs/`) i nie trafia do repo. Dokumenty linkują się nawzajem: decyzje PP, sekcje (np. DESIGN §5.3) i issues.
- **Dochodzi szósty dokument, `GUIDE.md`.** Przewodnik dla graczy i testerów, zbudowany wokół tego, co gracz faktycznie wybiera: wybory, krótkie segmenty, wykresy i pytania o opinię. Bez kodu, numerów zadań i tabel do strojenia. Służy do zbierania uwag od osób testujących grę.
- **Liczby i wykresy w przewodniku pochodzą z kodu.** Generator czyta stałe z plików C# przy każdym budowaniu (w tekście jako `{Klasa.Stała}`), więc przewodnik nie rozjeżdża się z grą. Stała, której już nie ma, przerywa budowanie (także w CI).
- Przewodnik nie zastępuje DESIGN ani VISION: decyzje i pełny projekt systemów zostają tam.

### Przyjęte 2026-10-05 (siedemnasta runda: pierwsze uwagi testerów)

**PP-057: Uwagi z pierwszego czytania przewodnika „Jak działa gra”** (zmienia PP-018 w części o wieku wejścia do puli; reszta to doprecyzowania DESIGN). Liczby poniżej to szacunki.
- **Siła historii w skali 0–10** zamiast 0–100. Różnicy między 55 a 56 nie da się odczuć. Losowość zostaje 0–100.
- **Juniorzy wcześniej, z akademią.** Ludzie wchodzą do puli talentów, gdy kończą karting i trafiają do serii juniorskich, czyli dziś około 14. roku życia. Wiek wejścia zależy od epoki, bo w latach 50. nie było drabinki juniorskiej: do 1969 około 18 lat, w latach 1970–1989 około 16, od 1990 około 14. Zespół ma akademię z ograniczoną liczbą miejsc (na start 3), a juniorzy rozwijają się w niej latami. Późni debiutanci (np. Fangio) wchodzą jak dotąd, kilka lat przed prawdziwym debiutem.
- **Płynność w latach do 1960 waży 12% oceny ogólnej** zamiast 14%. Dwa punkty przechodzą na zakręty: część kierowców była szybsza, jadąc bokiem.
- **Podwyżki w trakcie umowy.** Kierowca może zażądać podwyżki przed końcem kontraktu. Jak często, zależy od lojalności i morale: lojalny i zadowolony prosi rzadko. Kwota zależy od tego, ile kierowca jeszcze może zyskać: kierowca na szczycie, bez dużego zapasu potencjału, żąda mniej niż wschodząca gwiazda. Do czasu wprowadzenia morale liczą się lojalność i ostatnie wyniki.
- **Rozwój auta zależy od epoki.** Projektowanie koncepcji trwa dłużej, gdy auta są bardziej złożone (tak jak produkcja). We wczesnych epokach zyski z projektów są mniejsze, a koncepcja częściej kończy się porażką.
- **Popularność liczy też walkę o tytuł kierowców.** Wyrównana walka kierowców podnosi popularność nawet przy dominacji jednego zespołu (2016), ale mniej niż walka kilku zespołów (2010–2012).
- **Nagłe awarie.** Mniej więcej co trzecia awaria przychodzi bez ostrzeżenia, np. pęknięte zawieszenie albo wybuch silnika. Reszta jak dotąd daje kilka okrążeń sygnałów.
- **Słowa dla gracza:** „widełki” zamiast „pasmo”, „rozrzut czasów” zamiast „szum”.
- **Bez zmian na razie:** oceny prawdziwych kierowców zostają wstępne (wartość na wejściu i potencjał), dokładniejsze ustalimy później; spadek formy weteranów (np. Alonso) zostaje jak jest.

### Przyjęte 2026-10-05 (osiemnasta runda: co przenieść z Pelotona i Ping-Ponga)

**PP-058: Sprawdzone mechaniki z Ping-Pong Managera i lekcje z researchu Pelotona.** Liczby to szacunki.
- **Kierowca a oferty rywali.** Rywal może kusić naszego kierowcę dopiero w ostatnim roku jego umowy i nie wcześniej niż po kilku wyścigach sezonu. To, jak się o tym dowiemy, zależy od morale i lojalności kierowcy: niskie oznaczają „odchodzę po sezonie”, średnie „mam lepszą ofertę, przebijecie?” (zwykłe negocjacje), a wysokie „dostaję oferty, ale zostaję”. Pewne odejście tylko przy bardzo niskim morale i lojalności albo przy złamanej obietnicy z kontraktu. Przy gwiazdach plotka najpierw trafia do gazety. Razem z podwyżkami w trakcie umowy (PP-057) to jeden system.
- **Zarząd daje wybór celu przed sezonem:** bezpieczny (mała premia), oczekiwany albo ambitny (duża premia, porażka może kosztować posadę).
- **Cele sponsorów pasują do siły zespołu.** Słaby zespół dostaje osiągalne cele, a premia rośnie z trudnością celu i nigdy nie jest odwrotnie.
- **Akademia (rozwija PP-057).** Poziom akademii zmienia jakość juniorów, a nie ich liczbę (1–2 nowych na sezon). Akademia kosztuje co sezon. Mniej więcej 10% juniorów nie dochodzi do potencjału. Junior rozwija się szybciej, gdy się ściga. Bez minigry z treningiem: tylko decyzje.
- **Atrybuty starzeją się różnie.** Fizyczne (kondycja) szczytują wcześnie i spadają pierwsze, mentalne (opanowanie, regularność, informacja zwrotna) rosną najdłużej. Dlatego doświadczony kierowca bywa lepszy od młodszego.
- **Dwie krzywe balansu.** To, co pieniądze kupują (sztab, infrastruktura, działy), ma malejące korzyści, a koszt rośnie coraz szybciej. Talent kierowcy działa prawie liniowo, ale jest rzadki, starzeje się i drożeje.
- **Lista antywzorców** z researchu Pelotona służy do review każdej mechaniki (AGENTS.md).
- **Na później:** cechy zespołów (np. fabryczny, prywatny, „akademia”) i szef AI dobierany pod zespół.

### Przyjęte 2026-10-05 (dziewiętnasta runda: sztab pod MVP)

**PP-059: Uproszczony sztab na drodze do MVP** (doprecyzowuje DESIGN §6.2 i PP-044).
- **Wszystkie role sztabu są dostępne od 1950:** dyrektor techniczny, główny projektant, szef aerodynamiki, szef dynamiki pojazdu, inżynier wyścigowy (jeden na kierowcę, z relacją z kierowcą), strateg, szef mechaników, skaut, dyrektor komercyjny. Szef aerodynamiki w latach 50. daje niewiele, bo limit docisku epoki jest bliski zera, więc nie trzeba osobnej reguły.
- **Projektant silników pracuje u producenta silników, nie w sztabie zespołu.** Zespół, który jest własną fabryką silników (np. Ferrari), ma go w swojej fabryce (program silnikowy, PP-019). Klient, nawet klient numer 1, nie ma na niego wpływu i jedzie tym, co zrobi producent.
- **Szef zespołu to gracz albo AI, nie członek sztabu.** Ma atrybuty menedżera (PP-044) i pojawia się osobno.
- **Puste stanowiska wypełniają fikcyjni ludzie,** jak pulę kierowców. Znani prawdziwi ludzie zostają tam, gdzie ich znamy. Oceny personelu to na razie szacunki.

### Przyjęte 2026-10-05 (dwudziesta runda: MVP z UI)

**PP-060: MVP to sezon 1955 grywalny w prawdziwym oknie przez jednego gracza; multiplayer online jest zaraz po MVP** (doprecyzowuje PP-045 i PP-050; ustalone wcześniej, zapisane dopiero teraz).
- **MVP = sezon 1955 w przejętym zespole, grywalny w prawdziwym UI, dla jednego gracza.** Sama konsola z bramką fazy 4 (#113) to etap pośredni, a nie MVP.
- **UI powstaje równolegle z rdzeniem, falami:** ekran podłączamy do prawdziwej gry, gdy gotowy jest system, który pokazuje. Ekranu nie robimy przed systemem, który ma w nim żyć.
- **Podział:** most UI–rdzeń, okno i typy robi koder backendu; ekrany z prototypu przenosi sesja UI (Claude, PP-024).
- **Multiplayer jest po MVP i tylko online** (host i goście, TECH §5.1). Rdzeń nadal jest pisany pod wielu graczy (`managerId`, bramka gotowości, jedna kolejka komend), a obliczenia mają być identyczne na Windows i Linux.
### Przyjęte 2026-10-05 (dwudziesta pierwsza runda: kontuzje i zastępcy)

**PP-061: Kontuzje wyłączają kierowcę z wyścigów, a zespół wystawia zastępcę** (doprecyzowuje DESIGN §7 i uzupełnia PP-050).
- **Lekka kontuzja:** kierowca opuszcza 0–1 wyścig, a gdy jedzie, przez krótki czas jest trochę wolniejszy.
- **Poważna kontuzja:** kierowca pauzuje kilka wyścigów (szacunek 2–6).
- **Kończąca karierę i śmiertelna (przy włączonej opcji):** jak dziś, odejście ze sportu.
- **Zastępca:** to jedyny wyjątek od zasady PP-050 „bez zmian kierowców w sezonie”. Przed każdym wyścigiem bolid obsadza kierowca rezerwowy zespołu, a w razie braku – jednorazowy zastępca z wolnych agentów lub puli talentów. Gracz decyduje w skrzynce, AI szef wybiera i zostawia ślad. Gdy brak kandydata, bolid nie startuje. Regularny kierowca wraca automatycznie po wyzdrowieniu.
- **Wszystkie liczby to szacunki do strojenia.**

**PP-062: Bez testu wierności historii i bez kroniki rozbieżności** (zastępuje PP-012; usuwa kronikę rozbieżności z wizji, DESIGN §12, ROADMAP fazy 3 i 5 oraz TECH §8; decyzja właściciela z 2026-10-05).
- **Gra nie porównuje świata z prawdziwą historią.** Nie ma raportu wierności (rozkład mistrzów, dominacja względem rzeczywistości), bramki jakości opartej na nim ani ekranu, który zestawia Twoją oś czasu z prawdziwą. Gracz zna przeszłość sam.
- **Skutki decyzji są emergentne.** Bramka fazy 4 (#113) nie ustala z góry, ile rzeczy ma się rozjechać po innej decyzji. Raport pokazuje, co się rozjechało, a ocenia właściciel.
- **Gracz może przejąć dowolny zespół stawki.** Scenariusze testowe biorą zespół jako parametr, a nie gotowe „historie”.
- **Zostaje kalibracja silnika wyścigu** (`calibrate-race`, #122): odsetek ukończeń, awarie, przewagi. To strojenie mechaniki wyścigu, a nie ocena, kto powinien wygrać.

**PP-063: Menu, nowa kariera, zapis i ustawienia na MVP** (decyzja właściciela z 2026-10-05; doprecyzowuje PP-060).
- **Menu główne:** Kontynuuj (ostatni zapis), Nowa kariera, Wczytaj, Ustawienia, Wyjdź. W trakcie gry to samo menu pod Esc: Zapisz, Zapisz jako, Wczytaj, Ustawienia, Wyjdź do menu.
- **Nowa kariera w krokach:** Ty (imię, nazwisko, narodowość, profil szefa) → Świat (rok, ustawienie świata, „Zaawansowane” ze wszystkimi parametrami kariery) → Zespół → podsumowanie i „Rozpocznij”.
- **Zespoły wybiera się z ładnych kart ze wszystkim, co ważne:** kierowcy, auto i silnik, budżet, cel zarządu, siła w poprzednim sezonie. Karta pokazuje tylko to, co szef zespołu może wiedzieć.
- **Zapis tylko ręczny.** Bez autozapisu na MVP.
- **Ustawienia na MVP:** język. Barwy epoki lub zespołu, skórka epoki i animowane tło są „kiedyś”. Tryb opinii zamiast liczb też kiedyś; na razie zawsze liczby.
- **Na MVP wyścig liczy się w całości od razu,** bez pit-stopów i decyzji w trakcie. Rdzeń MVP to kontrakty, sztab, infrastruktura i rozwój bolidu.
- **Po MVP:** oglądany wyścig z prędkością wybieraną na ekranie wyścigu (nie w ustawieniach) i ręcznymi pit-stopami gracza. Strateg, który decyduje za gracza, jest jeszcze później.

**PP-064: Zakres MVP: dowolny zespół, dwóch kierowców, finanse, proste wyścigi; silniki bez fabryk** (decyzja właściciela z 2026-10-06; doprecyzowuje PP-050, PP-060 i PP-063).
- **Gracz wybiera dowolny zespół stawki.**
- **Każdy zespół ma dwóch głównych kierowców.** Rezerwowi mogą być, ale nie są ważni na MVP.
- **Finanse mają działać od pierwszego dnia.** Wyścigi są na razie symulowane w całości (PP-063).
- **Obiekty związane z silnikami (hamownia, odlewnia) i bycie zespołem fabrycznym albo producentem silników to osobna aktualizacja po MVP.** Na MVP każdy zespół jeździ silnikiem, który ma w 1955, a negocjacje dostaw silników czekają.
- **Osiągi silników zmieniają się trochę losowo co sezon** (każdy producent osobno, ze stałego strumienia losowego, wynik zależy od ziarna). Mocny silnik może osłabnąć, a słaby dogonić czołówkę. Wielkość zmiany to szacunek.
- **Infrastruktura na MVP:** fabryka, wynajem toru testowego i transport (ciężarówki, do Argentyny statek). Bez tunelu, symulatora i telemetrii w latach 50.

**PP-065: Każdy zespół ma własną pulę sponsorów, bez walki o sponsorów** (decyzja właściciela z 2026-10-06; doprecyzowuje PP-050 i PP-064).
- **Na razie każdy zespół ma własną pulę sponsorów, co sezon nowe i różne propozycje** (nazwy, kwoty, cele). Pula wynika z zespołu i sezonu, więc zespół gracza i zespoły AI grają tą samą regułą.
- **Zespoły nie walczą o tych samych sponsorów.** Żaden sponsor nie jest wspólny, więc umowa AI nigdy nie odbiera ani nie blokuje oferty gracza, a rywal nie podpisuje sponsora sprzed nosa. Sponsorzy z nazwy (z pliku autorskiego) są dostępni dla zespołu gracza co sezon.
- **Wspólny rynek sponsorów, z konkurencją, wejdzie później.** Liczby (ile propozycji na miejsce) to szacunek.
- **Zostaje:** oferta przedłużenia jest decyzją w skrzynce, wiadomość 60 dni przed końcem umowy, AI nie dostaje powiadomień.

**PP-067: Polecenia z boksu w trakcie oglądanego wyścigu, na razie w szybkim wyścigu** (wersja testowa na prośbę właściciela z 2026-10-07, do dalszej analizy; doprecyzowuje PP-029, PP-052 i PP-064).
- **W trakcie wyścigu gracz może przejąć auto od stratega:** ustawić tempo kierowcy w pięciu stopniach (od pełnego oszczędzania do tempa jak w kwalifikacjach), tryb silnika w trzech (oszczędny, normalny, pełna moc), włączyć polecenie zespołowe „przepuść kolegę” oraz zamówić zjazd z wyborem opon i go odwołać (doprecyzowanie właściciela z 2026-10-07). Domyślnie nadal wszystko robi strateg (PP-029), a gracz może mu tempo oddać.
- **Każdy tryb ma koszt powiązany z resztą wyścigu:** szybciej znaczy więcej zużycia opon i paliwa, tempo kwalifikacyjne także większe ryzyko wypadku, pełna moc częstsze awarie silnika. Wyścig bez poleceń zostaje dokładnie taki jak przedtem.
- **Na razie tylko w szybkim wyścigu,** bo tam wynik nie jest zapisany przed oglądaniem. W karierze wyścig nadal liczy się w całości w dniu wyścigu (PP-064), a boks pokazuje dane aut bez poleceń. Polecenia w karierze wymagają, żeby dzień wyścigu czekał na oglądanie; to osobna decyzja.
- **Polecenie nie zmienia tego, co gracz już widział.** Wyścig liczy się od nowa z tymi samymi losami i działa tylko na dalszą część.
- **Ekran wyścigu ma dawać coś do roboty i do oglądania:** radio kierowcy, dane własnych aut, walki o pozycję, komunikaty i auto-pauza. Kropki zwalniają w zakrętach (tylko wyświetlanie; czas okrążenia jak dotąd).
- **Koszty tempa i progi radia to szacunek** do kalibracji.

**PP-066: Rozwój auta v2: automat inżynierów, koncepcje na kilka sezonów, liczby czytelne dla gracza** (decyzja właściciela z 2026-10-07 po pierwszym teście; zastępuje w grze konto rozwoju i priorytety obszarów z PP-043, reszta PP-032, PP-043, PP-053 i PP-057 zostaje).
- **Części do auta wybierają i dowożą inżynierowie, tak samo w zespole gracza i AI.** Wynik zależy od ludzi: od umiejętności i od cechy **innowacyjność**. Tańszy, innowacyjny inżynier bywa nieprzewidywalny i czasem daje przełom, więc najlepiej opłacany sztab nie jest gwarancją najlepszego auta. Losowanie idzie ze strumienia `Development`.
- **Gracz wybiera tylko trzy rzeczy:** suwak podziału ludzi między auto, które jedzie, a następną koncepcję; charakter następnej koncepcji (ewolucja albo rewolucja, aerodynamika na proste albo na zakręty) z liczbami; moment wprowadzenia gotowej koncepcji. Nie ma konta rozwoju, priorytetów w procentach ani „sumy 100%”.
- **Koncepcja może jeździć kilka sezonów** (w latach 50. auto ewoluuje 2–3 lata). Nowa co rok nie jest wymuszona: ewolucja startuje blisko pułapu i ma wąski zakres, rewolucja startuje niżej, ma szeroki i wyższy zakres. Budowa gotowej koncepcji trwa 1–2 miesiące zależnie od epoki, a potem zastępuje starą razem ze spadkiem zrozumienia auta.
- **Prawda i wiedza.** Gracz i AI widzą cztery obszary (silnik, aerodynamika, prowadzenie, niezawodność) jako zakresy z oceny sztabu technicznego (lepszy sztab, węższy zakres) oraz zakresy czołowej trójki rywali jako wiedzę publiczną. Symulacja czyta prawdę. Zrozumienie auta rośnie z testami i kilometrami, a brak zrozumienia kosztuje osiągi.
- **Skrzynka:** nowa część (obszar, zakres przed i po, miejsce wobec czołowej trójki), gotowa koncepcja jako decyzja z liczbami (zysk, czas budowy, koszt, pierwszy wyścig), porażka i wejście koncepcji do auta. Koncepcje mają nazwy „zespół rok”, nigdy numer projektu.
- **Wszystkie liczby to szacunki do strojenia.**

**PP-068: Sponsorzy z warunkami umowy: długość, poziom warunku, życzenie narodowości, przedłużenie jako negocjacja** (decyzja właściciela z 2026-10-07 z playtestu 1, #268; doprecyzowuje PP-050, PP-058 i PP-065). Liczby to szacunki.
- **Jeden przepływ zamiast „miejsce, potem sponsor”.** Jedna lista wszystkich sponsorów, z którymi zespół może rozmawiać, sortowana po nazwie, branży, miejscu i kwocie rocznej. Miejsce na aucie wynika z rodzaju sponsora (pierwsze wolne, w które pasuje), więc gracz wybiera sponsora, a nie pustą rubrykę.
- **Umowa trwa od 1 do 3 lat.** Dłuższa płaci rocznie nieco mniej i daje spokój (nie trzeba szukać sponsora co roku). Kwota roczna jest stała przez całą umowę, a warunek sportowy jest liczony od nowa co rok, według siły zespołu w tym momencie.
- **Poziom warunku jest wyborem gracza:** łatwiejszy, standardowy albo trudniejszy. Trudniejszy cel płaci więcej, łatwiejszy mniej, a premia za spełnienie rośnie z trudnością celu i nigdy odwrotnie (PP-058). Cel jest zawsze dopasowany do oczekiwanej pozycji zespołu (ta sama wiedza co zarząd), więc zespół z czołówki dostaje wyższe cele. Sponsor bez celu sportowego ma jedną wersję umowy.
- **Życzenie narodowości jest rzadkie i jest premią, nie wymogiem.** Sponsor stawia je tylko wtedy, gdy na torze albo na rynku jest kierowca tej narodowości. Duży sponsor chce go w składzie wyścigowym, mały zadowoli się rezerwowym, a gra mówi to wprost. Spełnione życzenie dodaje premię do rat; niespełnione niczego nie psuje: bez utraty zaufania i bez końca umowy.
- **Niektóre branże dają dodatki:** paliwa, oleje, opony i motoryzacja dostarczają towar wyceniony na część kwoty rocznej z każdą ratą, banki, dobra konsumpcyjne i elektronika dopłacają raz z pierwszą ratą.
- **Kwoty były za małe.** Wszystkie pełne ceny mnożymy przez stałą skalę (szacunek). Nie ruszamy kształtu rynku, tylko jego poziom.
- **Przedłużenie jest negocjacją, jak kontrakt.** Zadowolony sponsor proponuje więcej niż płacił (im większe zaufanie, tym większa podwyżka). Gracz może zmienić długość i poziom warunku, a sponsor od razu wycenia nowe warunki i przysyła nową decyzję w skrzynce; takich zmian jest kilka, potem oferta jest ostateczna. Zostaje: wiadomość 60 dni przed końcem, brak odpowiedzi to koniec umowy, AI nie dostaje powiadomień.
- **Zostaje bez zmian:** własna pula na zespół (PP-065), czekanie poprawia warunki. Osobno do decyzji właściciela: czekanie nic nie kosztuje, dopóki rywal nie może podpisać pierwszy.
