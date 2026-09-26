# Przekazanie: prototyp UI, runda uwag właściciela (2026-09-26)

Dla nowej sesji (Opus 5.5, wyższy effort). Przeczytaj w całości, zanim zaczniesz. Uwagi właściciela są spisane wiernie. Tam, gdzie coś jest niejasne, zapytaj, zamiast zgadywać.

---

## 1. Kontekst w 30 sekund

- **Gra:** Paddock Principal, menedżer motorsportu z karierą od 1950 i prawdziwymi ludźmi.
  - Kierunek, decyzje PP-001…PP-040: `VISION.md`
  - Systemy gry (§14 to UI): `DESIGN.md`
  - Plan: `ROADMAP.md`
  - Zasady dla agentów: `AGENTS.md`
- **Właściciel** pisze po polsku, jest reżyserem i recenzentem, nie programistą. Poprzeczka: **„albo będzie fantastyczna, albo jej nie wydamy”**. Nienawidzi AI-slopu.
- **Stack UI:** HTML/CSS/TS w Photino (później). Teraz statyczny prototyp w czystym HTML/JS.
- **Prototyp:** `ui/prototype/`
  - `index.html`: powłoka;
  - `css/app.css`: system komponentów i tokeny;
  - `css/screens.css`: style ekranów;
  - `js/data.js`: atrapowe dane, Tyrrell 1976;
  - `js/ui.js`: klocki, w tym gwiazdki, flagi, paski;
  - `js/screens.js`: 21 ekranów;
  - `js/app.js`: router na hashach, ustawienia, tło.
- **Podgląd:** `.claude/launch.json` → konfiguracja `ui-prototype` (python http.server, port 5178). W panelu przeglądarki ustaw viewport **1620×860** i **1440×900**. Zrzut po zmianie rozmiaru bywa przycięty: ustaw rozmiar drugi raz i zrób zrzut ponownie.
- **Starsze makiety** w `ui/mockups/` to historia iteracji: a/b/c to kierunki, d–g to pulpit v2–v5.
- **Skille:** `impeccable` w `.claude/skills/impeccable` (zasady w `reference/craft-floor.md`, `typeset.md`, `animate.md`, `layout.md`), `web-design-guidelines` u użytkownika w `~/.claude/skills`.
  - **Uwaga:** impeccable zakłada wizualny `DESIGN.md` w katalogu głównym, a u nas `DESIGN.md` to dokument gry. Nie pozwól mu go nadpisać. Nie uruchamiaj `impeccable context/document/init` bez tej świadomości.

## 2. Co już ustalone i się podoba (NIE ruszać bez powodu)

- **Kierunek C („barwy epoki”)**, skórka 1976: krem, ramki, krój Big Shoulders Display i Archivo, paski epoki.
- **Kolory zespołu jako motyw** („kolory w punkt, idealnie”). Tyrrell (granat i czerwień), Lotus JPS (czerń i złoto), Ferrari.
- **Układ pulpitu v5:** skrzynka po lewej, wyścig i Paddock Monthly w środku, auto na tle stawki i mistrzostwa po prawej. Mieści się na jednym ekranie.
- **Duży przycisk „Dalej”** (jedyny przycisk postępu czasu, wciskany fizycznie).
- **Tło:** smugi dymu jak w tunelu aerodynamicznym. Design OK. Plamy odrzucone.
- **Gwiazdki 0–5 z połówkami zamiast oceny 1–100** (PP-040). Atrybuty zostają 1–20.
- **Ekran Kierowcy (skład)** „wygląda naprawdę fajnie”. Profil kierowcy z numerem na skośnym pasie też się podoba.

## 3. Zasady ogólne z tej rundy (stosuj wszędzie)

1. **ZAKAZ „głupich dopisków”.** Nie piszemy tekstów w stylu:
   - „8 za nami”, „sortuj, klikając nagłówek”;
   - „Dlaczego to nigdy się nie kończy”;
   - „Umowa z Goodyear kończy się w tym roku” w stopce;
   - „Kliknij kierowcę, żeby…”;
   - „Ocena naszego działu technicznego”.

   Gracz ma to **czuć i widzieć z UI**, a nie czytać instrukcję w twarz. Zamiast opisu używamy sygnału: data kończącej się umowy robi się czerwona, a nagłówek tabeli wygląda na klikalny. Wyjaśnienia mogą trafić do poradnika, nigdy na ekran.
2. **Konkret zamiast ogólników.** System mówi liczbami i wartościami, nie ocenami typu „Dobra jak na zespół prywatny” czy „przeciętnie”. Opinie w słowach mogą mieć tylko **ludzie** (cytat inżyniera w skrzynce albo w trybie bez liczb), nigdy system.
3. **Segmentuj.** Właściciel lubi informacje w segmentach, kafelkach i polach: etykieta nad wartością, wyraźne bloki. Nie lubi ciągłego tekstu, „nasranego” w jednym akapicie.
   - Przykład: „RSA · 26 l. · Kierowca #1 · Elf Team Tyrrell” mają być osobnymi segmentami, większymi.
4. **Wartości wielką literą:** „Wysoka”, „Dobre”, a nie „wysoka”, „dobre”.
5. **Wyrównanie:**
   - nie wszystko do lewej;
   - liczby i krótkie wartości w tabelach i statystykach wyśrodkowane albo spójnie do prawej;
   - kolumny tabel nie mogą być sztucznie rozciągnięte na całą szerokość z dziurami. **Masz tendencję do zbyt szerokiego układania wszystkiego.** Tabela ma mieć szerokość swojej treści albo rozsądnie rozłożone kolumny.
6. **Nie skracaj etykiet, gdy jest miejsce:** „Adaptacja”, „Opanowanie”, „Regularność”, a nie „Adapt.”, „Opan.”, „Regul.”.
7. **Polska odmiana liczebników:** 1 osoba, 2–4 osoby, 5+ osób (oraz wyścig/wyścigi/wyścigów itd.). Potrzebna funkcja `plural()`.
8. **Przyciski nie mogą być za małe** („Porównaj” i „Wróć do składu” były za małe).
9. **Ta sama „rodzina” kontrolek wszędzie.**
   - Pigułki („fasolki”) przy typie umowy u dostawców odstają od reszta UI.
   - Przełączniki typu F1 / Samochody sportowe / F2 w klasyfikacjach są brzydkie i „z dupy”.
   - Trzeba zaprojektować jeden porządny komponent zakładek/przełącznika i jeden styl etykiety statusu.
10. **Przejścia i „ciężar” kliknięć (bardzo ważne).** Przełączanie ekranów (Pulpit → Skrzynka → Kalendarz) jest **za szybkie i za puste**, „czuć taniość”, „gierka z HTML-a zrobiona przez AI”. Kliknięcia mają być **cięższe i dłuższe**: przemyślane przejście między ekranami (dłuższe, z charakterem, spójne z motywem), a przycisk ma reagować namacalnie.
    - **Wyjątek:** przełączanie maili w skrzynce ma być **natychmiastowe, bez animacji**. „Skrzynka to skrzynka.”
11. **Każdy element musi prowadzić do sensownego miejsca.** Wszystko, co wygląda na klikalne, ma działać i prowadzić tam, gdzie gracz się spodziewa, a nie do przypadkowego ekranu.
12. **Ważne decyzje:** zawsze wybór opcji, a potem osobny przycisk **„Potwierdź”**. Nigdy natychmiastowy efekt po kliknięciu opcji.

## 4. Uwagi ekran po ekranie

### Pulpit
- OK ogólnie.
- **Górny pasek jest „zbyt tekstowy”.** Data, wolne środki i gotówka powinny mieć **własną strefę/sekcję** o podobnym charakterze jak panele (Skrzynka, Monthly, Auto, Mistrzostwa), a nie luźny tekst.
- Właściciel proponuje benchmark: jak **Football Manager, Motorsport Manager, F1 Manager** rozwiązują górny pasek (data, pieniądze), sekcje i nawigację, pod kątem **UX, nie tylko wyglądu**. Może zainstalować MM albo F1M, jeśli to pomoże. Na razie poszukaj zrzutów.

### Skrzynka
- Przełączanie maili bez animacji (patrz §3.10).
- Decyzje z przyciskiem „Potwierdź”.

### Kalendarz
- **Usuń** dopisek „kto wygrał / które miejsce zajęliśmy” na kafelkach.
- **Usuń** podtytuł „16 rund · 8 za nami”. Wystarczy „Kalendarz 1976 · 16 rund” albo mniej.
- **Kafelek rundy:**
  - po lewej duży numer (jak teraz, podoba się);
  - obok data, kraj, tor, większe niż teraz („prawie OK”);
  - **po prawej mapka toru** (sylwetka układu, SVG).
- **Kliknięcie rundy otwiera stronę wyścigu:**
  - pełne wyniki;
  - wyniki z poprzednich lat na tym torze;
  - **statystyki toru:** średnia liczba DNF, średnia liczba samochodów bezpieczeństwa, lista zwycięzców, rekordy toru (np. rekord okrążenia).

### Klasyfikacje
- OK, ale **konstruktorzy ładują się dłużej** (animacja wejścia). Ma być równo.
- Przełącznik F1 / Samochody sportowe / F2 jest brzydki i „biedny”. Przeprojektuj (patrz §3.9) albo schowaj do czasu, aż ma sens.
- Podtytuł „Mistrzostwa świata F1 1976 · po 8 rundach” jakoś nie pasuje. Przemyśl.
- **Informacja o punktacji** (9-6-4-3-2-1, liczenie 7 najlepszych z każdej połowy) jest OK co do treści, ale **brzydko sformatowana**, jak zwykły tekst z Worda. Ma być zaprojektowana: segmenty i komponent.

### Kierowcy / profil
- **Klauzule** w kontrakcie wyglądają jak luźne wiadomości, bez ładu. Potrzebują struktury (typ klauzuli, warunek, skutek).
- **Atrybuty:**
  - pełne nazwy (§3.6);
  - wolne miejsce pod atrybutami trzeba wypełnić sensownie;
  - paski „mogą być na razie”.
- **Kariera:** OK, ale formatowanie „do lewej” jest słabe. Liczby wyśrodkować albo ułożyć w segmenty.
- **Numer kierowcy** na pasie jest lekko za bardzo w prawo.
- **Flaga** słabo widoczna, rozmazana. Potrzebne prawdziwe, wyraźne flagi (SVG).
- **Forma, morale:** wartości wielką literą.
- **Przyciski** „Porównaj” i „Wróć do składu” są za małe.
- **Linia „RSA · 26 l. · Kierowca #1 · Elf Team Tyrrell”** jest za mała. Ma być posegmentowana.
- **Profil przewija się o ~100 px** na 1620×860. Ma się mieścić.

### Porównanie
- **Przełącznik Kariera / Sezon 1976 / Pojedynki nic nie robi.** Ma działać.
- **Układ:** duże nazwiska i twarze po bokach (Scheckter po lewej, Depailler po prawej), „Atrybuty” na środku u góry. Teraz nie wiadomo, czyje są liczby.

### Personel
- **Nie da się wejść w profil osoby z personelu.** Ma się dać, analogicznie do profilu kierowcy.
- **Podtytuł „6 kluczowych osób · 34 pracowników w działach”** jest brzydko sformatowany, jak zwykły tekst.
- **Usuń notki przy osobach:**
  - „Autor P34”: niejasne, czy to „moje auto”;
  - „Przyszedł z Parnelli”: historię kariery gracz zobaczy w profilu i sam dopowie sobie fabułę.
- **Właściciel (Ken Tyrrell) na liście personelu ze statystykami to błąd.** Właściciel/szef to osobna rola (Zarząd), a nie pracownik pod projektantem. Pytanie otwarte: czy szef zespołu ma mieć atrybuty? Zaproponuj i zapytaj.
- **Działy są brzydko sformatowane:** liczba osób większa niż jakość, brak polskiej odmiany („1 osób”). Przeprojektować.

### Akademia
- **„Pula talentów”** i „najciekawsi według skauta” nie są klikalne. Mają prowadzić do profili i do puli.
- **Dwóch juniorów (Daly, South) wygląda na „wklejonych”.** Brakuje kontekstu, czym jest ich miejsce: slot? program? Przeprojektować tak, żeby było jasne, co to jest akademia, ile ma miejsc i jaki jest status każdego juniora, **bez dopisków-tłumaczeń**. Ma to wynikać ze struktury UI.

### Auto i rozwój: propozycja zmiany systemu (do omówienia z właścicielem)
Właściciel zastanawia się, czy **gracz nie powinien w ogóle wybierać konkretnych części**.
- Gracz ustala tylko **podział zasobów** (bieżące auto / konto rozwoju / przyszły rok, ewentualnie priorytety obszarów).
- O konkretnych projektach (silnik, zawieszenie itd.) **decydują inżynierowie**, na podstawie tego, kogo zatrudniono, ich doświadczenia, stażu w zespole i fazy adaptacji do struktury zespołu.
- **Gdy gracz zmienia podział, inżynier może zareagować w skrzynce**, np.:
  - „Dajcie nam jeszcze 2 tygodnie na obecny projekt, jesteśmy blisko przełomu”;
  - „Kończymy projekt, zaraz produkcja, uważamy, że się opłaca”.

  Gracz decyduje: trzyma się planu albo tnie budżet obecnego projektu na rzecz kolejnego.
- Pasuje do PP-029 (delegowanie) i PP-032. **Przed wdrożeniem przedstaw to właścicielowi jako decyzję projektową** (nowy wpis PP) i zaktualizuj DESIGN §5.3.

### Infrastruktura
- **„Wygląda bardzo brzydko.”** Przeprojektować od zera.
- **Usuń panel „Dlaczego to nigdy się nie kończy”** (§3.1). Gracz ma to czuć, a nie czytać.
- **Opisy typu „Dobra jak na zespół prywatny” zastąp konkretnymi, kwantyfikowanymi wartościami** (§3.2): poziom, parametry, porównanie z liderem w liczbach, koszt i czas rozbudowy.

### Dostawcy
- **Pigułki typu umowy** (Klient / Fabryczna / Partner) odstają od reszty UI.
- **Usuń kolumny Plusy/Minusy** z gotowymi ocenami („Pieniądze + paliwo za darmo”). Gracz ma sam ocenić na podstawie danych: parametry, koszt, warunki.
- **Usuń stopkę o kończącej się umowie.** Zamiast tego data końca umowy na czerwono, gdy to ostatni rok.

### Sponsorzy
- Treść OK, **okienka źle sformatowane / brzydkie**.

### Finanse
- „Nawet nie najgorsze”.
- **Pozycje wpływów i kosztów mają się rozwijać**, pokazując szczegóły: na co dokładnie poszły pieniądze.

### Zarząd
- **„Właściciel: Ken Tyrrell” jest dużo za małe.** Właściciel ma być wyraźną postacią na tym ekranie.

### Rynek kierowców
- **Nie da się wejść w profil kierowcy z rynku.** Musi się dać (profil obcego kierowcy z wiedzą skauta i pasmami).
- **Usuń „sortuj, klikając nagłówek”.** Sortowalność ma być widoczna z samego nagłówka.
- **Złe formatowanie:**
  - kierowca skrajnie po lewej, ocena i potencjał na środku, „kontrakt do” wyrównany do prawej;
  - za „Nastawieniem” ogromna pusta przestrzeń;
  - tabela sztucznie rozciągnięta.

  Zwarta, dobrze rozłożona tabela (inspiracja: rynek w Ping-Pong Managerze, „arkusz, który wygląda dobrze”).

### Paddock Monthly
- Wygląda „nawet w porządku”, ale:
  - **minimalne przewijanie, bardzo wkurzające**: albo ma się zmieścić, albo przewijać sensownie;
  - **działy (zakładki) nie są klikalne;**
  - **linki prowadzą bez sensu:** „Plotki transferowe” prowadzi na rynek, a technika, pieniądze i „z historii” do klasyfikacji. Każdy link ma prowadzić do właściwego miejsca (§3.11), np. plotka do profilu kierowcy lub listy plotek, technika do auta rywala, historia do kroniki.

### FIA i regulamin
- **„Kompletnie bez sensu”**, UI zbyt delikatne.
- **Głosowania nad przepisami (np. limit szerokości tylnych opon) mają przychodzić jako wiadomość w Skrzynce**, z opcjami i **przyciskiem „Potwierdź”** (§3.12). Nie jako przyciski na ekranie FIA.
- Ekran FIA niech pokazuje przepisy i historię zmian (do przeprojektowania).

### Ustawienia
- **Skórka epoki się nie zmienia**, bo przełącznik jest atrapą. Albo zaimplementuj (choćby 1976 / 1990s / 2020s), albo oznacz jako niedostępne.
- **Animowane tło jest ledwo widoczne:** wzmocnić, ale nie przesadzić. Wcześniej było za szybkie; teraz tempo OK, widoczność za mała.
- **Różnica kolorów Era vs Zespół jest prawie niewidoczna.** Ma być wyraźna.
- **Tryb bez liczb** się nie zapisuje i nie działa. W prototypie może być atrapą, ale przynajmniej zapisz wybór. Lepiej pokaż jeden ekran, np. profil kierowcy, w wersji opisowej.
- Oglądanie wyścigu i pit-stopy: OK.

## 5. Materiały inspiracji od właściciela
Opcjonalne. „Jak się nie przydadzą, to luz.”
- https://component.gallery/: katalog wzorców komponentów (zakładki, tabele, przełączniki). Przydatne przy §3.9.
- https://www.cta.gallery/: przyciski i wezwania do działania.
- https://vibeprompts.dev/
- https://21st.dev/: biblioteka komponentów (React).
- https://designmd.ai/: design systemy w formie `DESIGN.md`.
- https://kinetics.colorion.co/: ruch i animacje. Przydatne przy §3.10.
- https://ui.aceternity.com/: efektowne komponenty (React/Framer). Ostrożnie, łatwo o przesadę.
- Wcześniej: posty o „juice” (dźwięk, ruch, mikroreakcje), skill libraries.dev (efekty dla aplikacji AI), konstruktywistyczne UI Peloton Managera (github.com/DatJikun/peloton-manager, plik `peloton-manager-full-ui-poc-v3.html`).
- Benchmark UX: Football Manager, Motorsport Manager, F1 Manager.

## 6. Sugerowana kolejność pracy
1. **Fundamenty systemu komponentów:**
   - jeden przełącznik/zakładki;
   - jeden styl statusu (zamiast pigułek);
   - segmentowane „pola informacji”;
   - `plural()`;
   - flagi SVG;
   - przejścia ekranów z ciężarem (skrzynka bez animacji);
   - przycisk „Potwierdź” dla decyzji.
2. **Górny pasek pulpitu** jako strefa (po krótkim researchu FM/MM/F1M).
3. **Przegląd całego prototypu pod §3** (usunąć wszystkie dopiski, wyrównania, szerokości, skróty).
4. **Ekrany do przebudowy:** Infrastruktura, Dostawcy, Personel (+ profil osoby), Porównanie, Kalendarz (+ strona wyścigu z mapką toru), Rynek (+ profil z rynku), FIA (głosowania w skrzynce), Akademia, Zarząd.
5. **Auto i rozwój:** najpierw przedstaw właścicielowi propozycję systemu z §4, dopiero potem UI.
6. **Weryfikacja:** zrzuty na 1440×900 i 1620×860, zero przewijania poza naturalnie długimi listami, zero błędów w konsoli. Potem pokaż właścicielowi.

---

## 7. Następny temat właściciela: system awatarów (zgłoszony 2026-09-26)

Właściciel chce ładne awatary. Dotychczasowe próby wypadły słabo: w Peloton Managerze żadna się nie udała (`experiments/avatar_prototype` w repo Pelotona), w Ping-Pong Managerze na 4/10 (`tools/avatar-preview.html`, `tests/avatars.test.js`). Przed startem obejrzyj oba repozytoria i nazwij, co tam nie zagrało.

**Proponowany kierunek (do potwierdzenia na starcie):**
- **Kierowcy to przede wszystkim kask.** W motorsporcie to tożsamość (żółty kask Senny, wiosła Hilla).
  - Kask proceduralny z ziarna: kształt zależny od epoki (otwarty z goglami w latach 50.–60., integralny od lat 70., później szybka i halo), wzór (pasy, łuki, gwiazdy, motyw flagi), 2–3 kolory, numer.
  - Omija problem prawa do wizerunku, bo nie rysujemy prawdziwych twarzy.
  - Prawdziwi kierowcy mogą dostać ręcznie przygotowane wzory „w stylu” swoich kasków.
- **Personel i menedżer:** stylizowane portrety w płaskim stylu plakatu z lat 70., bez ambicji realizmu. Muszą pasować do skórki epoki i barw zespołu.
- **Technika:** SVG składane z warstw, deterministyczne z ziarna osoby (INV-002). Kilka rozmiarów: wiersz tabeli 32–40 px, karta, profil. Test: siatka 100 losowych awatarów jako „arkusz kontaktowy” do oceny właściciela.

**Pytania na start:** czy kask ma być głównym wizerunkiem kierowcy? Czy twarze w ogóle są potrzebne? Jaki styl portretów: plakat, rysunek, sylwetka? Najpierw 2–3 próbki stylu, dopiero potem system.

