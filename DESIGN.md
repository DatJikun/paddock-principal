# Paddock Principal — DESIGN

**Status:** DRAFT (2026-09-25)
**Rola:** jak działa gra: systemy, zasady i powody. Liczby w tym dokumencie to **szacunki do kalibracji**, a nie ustalenia.
Kierunek i decyzje: [VISION.md](VISION.md). Technika: [TECH.md](TECH.md).

---

## 1. Model świata

Świat to jedna ciągła symulacja od roku startu. Nie ma w nim „sezonów-paczek”.

| Byt | Co to jest | Uwagi |
|---|---|---|
| **Osoba** | kierowca, personel, menedżer, właściciel | Jedna osoba może zmieniać rolę w życiu: Brabham, McLaren, Surtees i Prost byli kierowcami, a potem szefami własnych zespołów. |
| **Organizacja** | zespół, producent silników, dostawca opon, sponsor, organizator serii | Ma datę powstania, zmiany nazwy, sprzedaż i koniec. Linia następstwa jest zapamiętana (Tyrrell → BAR → Honda → Brawn → Mercedes). |
| **Seria / sezon** | mistrzostwa z regulaminem epoki | Na start **tylko mistrzostwa świata F1** (bez wyścigów poza mistrzostwami). Inne serie w późniejszych fazach. |
| **Tor** | tor z **wersjami układu** | Nürburgring Nordschleife, stara i nowa Spa, stare Hockenheim to osobne wersje z własnym profilem. |
| **Samochód** | projekt na sezon plus rozwój w trakcie | Patrz §5. |
| **Epoka** | wpis osi czasu | Przepisy, punktacja, bezpieczeństwo, ekonomia (§4). |

---

## 2. Tryb historyczny

### 2.1. Punkt wejścia do świata (PP-018)
Nie symulujemy całej drabinki motorsportu, od kartingu dla 6-latków w górę. Ludzie pojawiają się w świecie w **punkcie wejścia**, czyli na najniższym modelowanym szczeblu drabinki. Na start modelujemy **tylko mistrzostwa świata F1**, więc punktem wejścia jest **pula talentów**: abstrakcyjny „świat poza F1”, czyli juniorzy, kierowcy innych serii i testerzy.

- **Kiedy prawdziwy kierowca trafia do puli:** 2–3 lata przed prawdziwym debiutem w F1, nie wcześniej niż w wieku ~17 lat. Dokładną liczbę lat ustalimy przy kalibracji. Weterani innych serii (np. Indy, sportowe samochody) wchodzą w wieku, w jakim byli naprawdę.
- **Co się dzieje w puli:** talent rozwija się sam, w tempie zależnym od potencjału i losu. Zespoły mogą go podpisać jako kierowcę wyścigowego, testowego albo „juniora” z opcją na przyszłość.
- **Inwestycja w juniora:** można opłacić juniorowi sezon „gdzieś niżej” (abstrakcyjny program: tanio i wolno, albo drogo i szybko). To przyspiesza jego rozwój i buduje lojalność. Kierowca rezerwowy rozwija się przez prywatne testy, ograniczone regulaminem epoki.
- **Wypełniacze:** pulę uzupełnia generator wiarygodnymi fikcyjnymi kierowcami. Bez nich pula byłaby listą przyszłych mistrzów, a wybór pozbawiony sensu. AI nie wie, kto jest „prawdziwy”.
- **Rozszerzenie ladderu:** gdy dodamy F2/F3 (faza 7+), punkt wejścia przesunie się niżej, a pula zamieni się w prawdziwe serie. Ten sam mechanizm, bez przepisywania.

Jeśli nikt nie da prawdziwemu kierowcy szansy, jego kariera może się nie wydarzyć. **To jest cecha gry, nie błąd.** Trafia wtedy do Kroniki rozbieżności.

### 2.2. Wiedza o przyszłości to część zabawy
Gracz zna historię i wolno mu z niej korzystać, np. podpisać Sennę do Lotusa i odbudować z nim potęgę z lat 60. i 70. albo jako Ferrari wybrać Häkkinena zamiast Schumachera. AI tej wiedzy nie ma (D-010) i działa na podstawie scoutingu. Jak bardzo świat „trzyma się” historii, ustawia gracz w konfiguracji kariery (§2.3).

### 2.3. Konfiguracja kariery (PP-005, PP-018)
Ustawienia przy tworzeniu kariery. Presety mają nazwy (np. **„Najbardziej historyczny”**, „Zbalansowany”, „Piaskownica”), ale każdy suwak można zmienić ręcznie:
- **Los legend:**
  - *Potencjał*: prawdziwa kariera wyznacza sufit talentu (z niepewnością), a osiągnięcie go zależy od świata: samochodu, wyników, sztabu, pewności siebie, kontuzji;
  - *Trajektoria*: umiejętności podążają rok po roku za wyliczoną krzywą prawdziwej kariery; świat zmienia wtedy, *gdzie* kierowca jeździ, ale nie *jak dobry* jest.
- **Siła historii (0–100%):** jak chętnie aktorzy AI realizują propozycje historyczne (§2.4), gdy są sensowne.
  - Przy 100% świat bez udziału gracza idzie torem prawdziwej historii, a rozjeżdża się tam, gdzie gracz zainterweniuje. Jeśli zabierzesz Mercedesowi miejsce dla Hamiltona, Mercedes weźmie np. Alonso.
  - Przy 0% jest czysta symulacja.
- **Suwak losowości:** rozwój, forma, awarie.
- **Śmiertelność** (PP-006), **rok startu**, **zespół** (istniejący albo własny, §3).

### 2.4. Zdarzenia historyczne jako propozycje (PP-004)
Prawdziwe zdarzenie w danych ma **warunki sensowności**, a nie wymuszenie. Przykład:

> *Schumacher przechodzi do Ferrari (koniec 1995).* Propozycja pojawia się tylko wtedy, gdy Ferrari istnieje, ma budżet i nie ma mistrza w składzie, a Schumacher ma kontrakt do zakończenia lub klauzulę. W przeciwnym razie zdarzenie przepada i ląduje w Kronice.

Rodzaje propozycji: powstanie zespołu (Brabham 1962, McLaren 1966, Williams 1977), wejście lub wyjście producenta, wejście sponsora, zmiana regulaminu, sprzedaż zespołu. Decyzję podejmuje odpowiedni aktor (AI albo gracz) na zwykłych zasadach. Propozycja jedynie podsuwa mu tę opcję, a „siła historii” (§2.3) podbija jej użyteczność dla AI.

### 2.5. Po 2026
Gdy prawdziwych ludzi zabraknie, generator tworzy nowych w tym samym rytmie i o tym samym rozkładzie talentu, jaki miały ostatnie dekady. Gracz nie powinien zauważyć szwu.

---

## 3. Gracz: menedżer i własny zespół

Gracz jest **osobą** (PP-007). Na starcie wybiera jedno z dwóch.

### 3.1. Praca w istniejącym zespole
Oferty zależą od reputacji menedżera. Zarząd stawia cele, a za słabe wyniki można zostać zwolnionym i szukać nowej pracy.

### 3.2. Założenie własnego zespołu (start kariery)
Kreator ustawia:
- **budżet startowy** (także jako poziom trudności), bazę (kraj) i dostawcę silników (klienckie umowy epoki);
- **pakiet sponsora założycielskiego z warunkami.** Przykłady:
  - kierowca określonej narodowości w składzie,
  - talent z określonej serii albo kraju w ciągu N lat,
  - podium w ciągu 3–5 lat,
  - utrzymanie budżetu powyżej zera.

  Spełnienie warunku przynosi pieniądze i zaufanie sponsora, a niespełnienie oznacza jego odejście.

To zarazem pierwszy system celów: cel, termin i skutek są znane od początku (STATE / WHY / FORECAST).

### 3.3. Założenie zespołu w trakcie kariery
Są dwie ścieżki. Ich trudność wynika z historii, a nie ze sztucznego limitu rocznego:
1. **Nowy zespół od zera.** Tanie i częste do końca lat 80. W latach 90. coraz droższe, bo dochodzą umowy Concorde i wymogi FIA. Od 2002 obowiązuje depozyt wejściowy, w 2010 był przetarg FIA, od 2021 jest opłata anti-dilution. W praktyce ścieżka jest realna głównie do lat 90., zgodnie z Twoją intuicją, ale nie jest zakazana później.
2. **Wykup upadającego zespołu:** możliwy w każdej epoce (Brawn 2009, Racing Point 2018). Wykup zmienia nazwę i przejmuje ludzi, samochód oraz długi.

Oferty są rzadkie i pojawiają się jako okazje w skrzynce (np. „zespół X upada, właściciel szuka kupca”). Szczegóły to osobny etap projektowania w fazie 4.

### 3.4. Własność i udziały (PP-028)
Zespoły są prywatne albo mają udziałowców. Można sprzedać część udziałów, żeby zdobyć gotówkę (inwestor dostaje wpływ i oczekiwania). Można też kupić udziały w innym zespole, np. zespół B do rozwoju juniorów albo inwestycję. Historycznie: Fiat kupuje Ferrari (1969), Red Bull i Toro Rosso, Mercedes kupuje Brawn (2009).

**Nie da się przejąć całej F1.** Limity wynikają z regulaminu (ile zespołów może kontrolować jeden podmiot, np. najwyżej dwa), ze zgody organizatora serii i właścicieli oraz z reakcji rynku, bo sponsorzy i producenci nie lubią monopolu. W trybie piaskownicy limity można wyłączyć dla zabawy.

---

## 4. Epoki i technologie

### 4.1. Oś czasu epok (PP-008)
Każdy wpis zmienia parametry od danego sezonu. Przykłady z historii F1:

| Od | Zmiana | Wpływ na grę |
|---|---|---|
| 1950 | punkt za najszybsze okrążenie (do 1959), dzielenie punktów przy zmianie kierowcy (do 1957), liczy się N najlepszych wyników (do 1990), Indy 500 w mistrzostwach (do 1960) | punktacja |
| 1950+ | pieniądze startowe od organizatorów wyścigów, bardzo wysoka awaryjność, niskie bezpieczeństwo | ekonomia, niezawodność, ryzyko |
| 1968 | sponsorzy na samochodach (Gold Leaf Team Lotus) | nowe źródło dochodu |
| 1981 | umowa Concorde, pieniądze z TV | ekonomia |
| 1984 | zakaz tankowania w wyścigu (znów dozwolone 1994–2009) | strategia |
| 1993 | samochód bezpieczeństwa | przebieg wyścigu |
| 2009+ | budżety ograniczane (limit wydatków od 2021) | ekonomia |

Pełną oś czasu budujemy w fazie 1 razem z danymi (**zadanie badawcze dla Groka**, patrz ROADMAP). Katalog zasad jest wspólny dla trybu historycznego (prawdziwe daty) i proceduralnego (zasady proponowane i głosowane). Przykłady spoza oczywistych: odwrócona kolejność startowa albo odwrócona kolejność mistrzostw, balast za sukcesy, zasady typu spec (silnik, zawieszenie, ECU, części standardowe), monopol vs wojna oponiarska, samochody klienckie dozwolone lub nie, podwójne punkty w finale (2014), wyścigi sprinterskie, zakaz poleceń zespołowych (2002–2010), parc fermé (od 2003), zakaz testów w sezonie (od 2009), ruchomy limit testów aero zależny od pozycji w mistrzostwach (od 2021), limity jednostek napędowych z karami. Lista wymiarów regulaminu do sparametryzowania: samochód bezpieczeństwa (brak / fizyczny / też wirtualny), przydział opon i mieszanki, masa minimalna, pomoce elektroniczne (kontrola trakcji, ABS, aktywne zawieszenie, aktywna aerodynamika), DRS, ERS, tankowanie i pojemność zbiornika, punktacja, format kwalifikacji, podział nagród, limity testów, limit wydatków, silniki (pojemność, turbo, hybryda).

**Polityka regulaminowa:** w erach, w których zespoły współdecydują o przepisach (np. Komisja F1), zmiany są proponowane i głosowane. Zespoły głosują zgodnie z własnym interesem, a gracz może lobbować; atrybut szefa „polityka” ma tu znaczenie. W trybie historycznym prawdziwe zmiany przychodzą jako propozycje (PP-004).

### 4.2. Drzewo technologii (PP-009)
Technologie mają prawdziwą datę wprowadzenia. Dla AI to punkt odniesienia, a gracz może ją wyprzedzić albo się spóźnić.

| Technologia | Prawdziwy debiut | Efekt |
|---|---|---|
| Silnik za kierowcą | Cooper (wygrane od 1958, tytuł 1959) | masa i prowadzenie |
| Monokok | Lotus 25 (1962) | sztywność, bezpieczeństwo |
| Skrzydła | 1968 | docisk przestaje być zerowy |
| Efekt przyziemny | Lotus 78/79 (1977–78) | ogromny docisk, dopóki nie zostanie zakazany |
| Turbo | Renault (1977), zakaz od 1989 | moc kosztem niezawodności |
| Monokok z włókna węglowego | McLaren MP4/1 (1981) | sztywność, bezpieczeństwo |
| Półautomatyczna skrzynia | Ferrari 640 (1989) | czas okrążenia, niezawodność |
| Aktywne zawieszenie | Lotus 1987, Williams 1992, zakaz 1994 | docisk i przyczepność mechaniczna |
| Hybryda | KERS 2009, jednostki napędowe 2014 | moc, koszty |

Technologia wymaga badań (personel i pieniądze) i ma ryzyko porażki.

**Kopiowanie wynika z ludzi, a nie z systemu szpiegowskiego** (PP-028). Inżynierowie podpatrują widoczne rozwiązania rywali (skrzydła, podłoga, koncepcja). Szansa i tempo skopiowania zależą od ich umiejętności analizy i kreatywności oraz od tego, jak bardzo rozwiązanie jest widoczne: skrzydło widać, mapowanie silnika nie. Wiedzę przenoszą też **ludzie zmieniający zespół**, bo projektant przechodzący do rywala zabiera doświadczenie z poprzedniej koncepcji. Osobnego systemu szpiegostwa nie ma. **Zakazy reagują na świat:** propozycja zakazu pojawia się, gdy dana technologia zbyt mocno dominuje w *Twoim* świecie, a nie w tym prawdziwym roku.

### 4.3. Nieskończona infrastruktura (PP-026)
Nie ma końca drzewka. **W 1972 nie może być endgame'u.** Trzy mechanizmy:
1. **Ruchoma granica technologii.** Jakość obiektu (tunel, hamownia, symulator, CFD) liczy się **względem stanu sztuki danego roku**, a ten stale przesuwa się do przodu razem z R&D całego świata. Najlepszy tunel w 1975 jest przestarzały w 1990. Obiekty starzeją się względnie i wymagają modernizacji albo wymiany.
2. **Nowe rodzaje obiektów z epokami:** hamownia → tunel aerodynamiczny → pełnoskalowy tunel → CFD → symulator kierowcy → hamownia jednostki hybrydowej itd. Każdy otwiera nowy wymiar rozwoju.
3. **Zmiany regulaminu i limity:** reset przepisów częściowo zeruje przewagę wiedzy. Od ery limitów (godziny w tunelu, limit wydatków) wygrywa efektywność, a nie sama wielkość.

---

## 5. Samochód (PP-027)

Budowanie auta ma być jednym z najlepszych systemów gry. Zasada: **nie ma jednej dobrej odpowiedzi**, bo każda droga i każdy wybór ma plusy i minusy, a ich wartość zależy od toru, regulaminu, budżetu i ludzi.

### 5.1. Skąd bierzesz samochód
Trzy drogi, dostępne zależnie od serii i epoki:

| Droga | Plusy | Minusy | Kiedy |
|---|---|---|---|
| **Samochód kliencki** (kupujesz gotowe podwozie) | tanio, szybko, znana baza, dobry start dla nowego zespołu | sufit osiągów wyznacza producent, zawsze jesteś krok za zespołem fabrycznym, rozwój ograniczony | F1: lata 50.–70. (Cooper, Lotus, Brabham, March, Lola), zakaz od umowy Concorde (1981); GT3: zawsze |
| **Własna konstrukcja** | pełna kontrola, sufit tylko w Twoich ludziach, możliwa przewaga technologiczna | drogo, ryzyko nieudanego projektu, wymaga infrastruktury i projektantów | F1: zawsze, od 1981 obowiązkowo |
| **Umowa z producentem**: od klienta, przez partnera, po zespół fabryczny | pieniądze, części, inżynierowie i silnik skrojony pod auto | zależność, cele producenta, ryzyko wycofania się, obowiązki marketingowe | silniki w F1, samochody w GT (szczegóły: specyfikacja GT3 z wcześniejszych ustaleń, faza 7+) |

W GT3 samochód ma **gotową, homologowaną specyfikację (plus BoP)**. Wynik zależy od tego, ile osiągów wyciągną z niego kierowcy, inżynierowie i mechanicy, a także od umowy z producentem. To samo jądro gry, tylko z inną drogą pozyskania auta.

### 5.2. Własna konstrukcja: koncepcja
Projekt roczny to zestaw decyzji na osiach, w których **każdy biegun ma swoją cenę**:

| Oś | Jedna strona | Druga strona |
|---|---|---|
| Aero | mały opór: szybko na prostych | duży docisk: szybko w zakrętach, większe zużycie opon |
| Filozofia | ewolucja: pewna, przewidywalna | rewolucja: wysoki sufit, ryzyko porażki, rywale mogą skopiować |
| Okno pracy | szerokie: łatwe ustawienia, stabilne na każdym torze | wąskie: bardzo szybkie w oknie, bezradne poza nim |
| Chłodzenie i niezawodność | zapas: mniej awarii, trochę wolniej | na krawędzi: szybciej, więcej awarii w upale |
| Opony | łagodne dla opon: dłuższe stinty | agresywne: szybkie okrążenie, słabsze na dystansie |
| Integracja silnika | pod konkretny silnik: optymalnie | uniwersalnie: łatwa zmiana dostawcy |

**Dopasowanie kierowcy do auta:** każdy kierowca ma preferencje prowadzenia (np. balans: podsterowność ↔ nadsterowność, trakcja: miękka ↔ ostra). Koncepcja auta ma swoją charakterystykę, a rozjazd kosztuje tempo i pewność siebie. Mistrz może być przeciętny w aucie, które mu nie leży. To jednocześnie decyzja projektowa („budujemy auto pod naszą gwiazdę czy pod partnera?”) i transferowa.

Do tego wybrane technologie z drzewa (§4.2). **Jakość wykonania** zależy od personelu (§6.2), infrastruktury (§4.3) i budżetu. Kierowca z dobrą informacją zwrotną pomaga rozwijać auto w sezonie.

### 5.3. Rozwój: potencjał koncepcji, konto rozwoju, moment wdrożenia (PP-032)

**Nie ma wspólnej krzywej, na której „stoi” auto.** Tempo rozwoju wynika z **zapasu własnej koncepcji**:
- **Sufit (potencjał) koncepcji:** każda koncepcja ma własny. Zależy od pomysłu (osie z §5.2 i technologie), od regulaminu i od jakości ludzi, którzy ją wymyślili. Nikt nie zna go dokładnie. Dyrektor techniczny podaje szacunek z niepewnością, a jego trafność zależy od umiejętności.
- **Zysk z pracy** jest proporcjonalny do **pozostałego zapasu** (sufit minus obecny poziom) oraz do zasobów, ludzi i infrastruktury. Blisko sufitu każda dziesiątka sekundy kosztuje coraz więcej. Nie ma premii za bycie „w środku krzywej”.
- **Skutek:** zespół, który trafił w lepszą koncepcję, rozwija się dłużej. Zespół z przeciętną koncepcją szybko się zatrzymuje i musi podjąć decyzję o zmianie (jak Mercedes z koncepcją „zero sidepods” w 2022). Stawka ściska się naturalnie, gdy wszyscy zbliżają się do podobnych sufitów w dojrzałych przepisach. Dominacje rodzą się z trafionych koncepcji (podwójny dyfuzor w 2009), a nie z pozycji na krzywej.
- **Nowy regulamin:** nowe koncepcje, nowe sufity. Część wiedzy przechodzi (ludzie, narzędzia, rozumienie opon), a część przepada.

**Konto rozwoju („oszczędności”):** nie każdą pracę trzeba od razu zamieniać na części do bieżącego auta. Część wyników badań można odłożyć na konto wiedzy i wykorzystać później: w kolejnej poprawce, w nowej koncepcji albo w aucie na przyszły rok. Konto traci wartość, gdy regulamin się zmienia, bo wiedza o zakazanym rozwiązaniu jest nic niewarta. Trzymanie wszystkiego na zapas też kosztuje, bo rywale uciekają.

**Nowa koncepcja jako projekt, z wdrożeniem kiedy chcesz** (i kiedy pozwala regulamin):
- W trakcie sezonu, gdy projekt jest gotowy. Wtedy zrozumienie auta spada do zera i trzeba je odbudować, a epoka może to blokować (homologacja podwozia, zamrożenia, limity testów).
- Po kilku wyścigach, gdy auto jest już przetestowane i zrozumiane.
- Dopiero w przyszłym sezonie, jako baza nowego auta.

Decyzja „czekać czy wdrażać” to kompromis: wcześniejsze wdrożenie daje punkty teraz, ale kosztuje zrozumienie i ryzyko. Podział zasobów między poprawki bieżącego auta, konto rozwoju i projekt nowej koncepcji ustawiasz sam albo zostawiasz dyrektorowi technicznemu (PP-029).

**Ilu ludzi i jak dobrzy (zasada ogólna dla rozwoju):** liczba ludzi przypisanych do projektu decyduje o **czasie**. O **jakości** decydują kluczowi ludzie, infrastruktura i budżet. Dorzucenie 50 inżynierów skraca projekt, ale nie robi z przeciętnej koncepcji genialnej.

**Nowe auto startuje z części własnego potencjału.** Z koncepcji na przyszły rok samochód dostaje na start tylko pewien procent sufitu, a resztę trzeba wyrobić rozwojem w sezonie. Filozofia projektu przesuwa ten kompromis:
- ostrożna: mocniejszy start, niższy sufit;
- eksperymentalna: słabszy start, wyższy sufit, większy rozrzut.

To dokładnie pasuje do modelu zapasu opisanego wyżej.

**Moment zamrożenia koncepcji:** projekt na przyszły rok bierze za punkt wyjścia stan auta w chwili zatwierdzenia koncepcji. Poprawki wprowadzone później pomagają już tylko temu sezonowi. Decyzja „kiedy zamrozić” jest więc realnym wyborem, a konto rozwoju pozwala część późniejszej pracy przenieść dalej.

**Wcześniejsze zakończenie projektu:** każdy projekt można zamknąć przed czasem. Dostajesz wtedy proporcjonalną część efektu. Przydaje się, gdy zbliża się ważny wyścig albo kończą się pieniądze.

**Zrozumienie części:** nowa część czy nowy pakiet nie daje pełnych osiągów od razu. Zespół musi go **zrozumieć** przez testy, kilometry w wyścigach i pracę inżynierów (procent zrozumienia na projekt). Limity testów z regulaminu sprawiają, że wprowadzenie dużej poprawki w połowie sezonu to realny koszt.

### 5.4. Osiągi, silnik, opony
- **Wektor osiągów:** Moc, Docisk (ograniczony epoką i technologią), Przyczepność mechaniczna, Hamowanie, Niezawodność. Dopasowanie do toru to iloczyn skalarny z wagami profilu toru.
- **Silnik:** kliencki albo **własny** (PP-019). Gracz może zostać producentem jak Ferrari, BRM czy Honda:
  - potrzebny jest projektant silników, osobny budżet i co najmniej sezon na pierwszy silnik;
  - silnik można sprzedawać klientom: to przychód, wpływy i dane, ale też rywale na tym samym sprzęcie;
  - zmiana przepisów (pojemność, turbo, hybryda) może unieważnić projekt.
- **Dostawcy (PP-030):** opony, paliwo i olej, kluczowe części (np. hamulce, skrzynia, elektronika), silniki.
  - **Rodzaje umów:**
    - fabryczna / partnerska: wspólny rozwój, pierwszeństwo w nowościach, pieniądze albo rabaty, obowiązki marketingowe, zależność;
    - kliencka: tanio i elastycznie, ale produkt „dla wszystkich” i zawsze krok za partnerem.
  - **Umowy wieloletnie** budują relację: im dłużej i lepiej, tym więcej wspólnego rozwoju (np. Ferrari i Shell).
  - **Zespół numer 1 u dostawcy** (najlepszy wśród jego klientów) współtworzy z nim technologię i dostaje ją pierwszy. Inni klienci dostają ją później albo w słabszej wersji.
  - **Każdy dostawca ma swoje plusy i minusy** (np. paliwo mocniejsze, ale mniej wydajne; opona szybka, ale kapryśna). Wybór jest kompromisem, a nie rankingiem.
  - **Silnik zeszłoroczny:** najtańsza opcja awaryjna. Tańszy, ale wolniejszy i bez rozwoju (historycznie np. Toro Rosso).
  - **Umowa partnerska może kosztować udziały:** producent daje darmowe albo tańsze silniki w zamian za udział w zespole na czas kontraktu (§3.4). Partner dostaje też prawo wpływu na kierunek rozwoju dostawcy (moc, niezawodność, wydajność) i może go dofinansować.
  - **Rozwój dostawcy przez zespół fabryczny poprawia produkt dla wszystkich jego klientów,** więc pomagasz też rywalom na tych samych oponach czy silnikach.
  - **Wojny oponiarskie** (Goodyear–Firestone, Michelin–Bridgestone): opona dostrojona pod zespół partnerski potrafi zmienić układ sił.

---

## 6. Ludzie

### 6.1. Kierowca: profil (PP-013, PP-031)

**Tożsamość:** imię i nazwisko, narodowość, data urodzenia i wiek, numer, wizerunek, kraj bazy fanów.

**Atrybuty widoczne (1–20) + ocena ogólna (1–100)** (PROPOZYCJA po uwagach właściciela):
| Atrybut | Znaczenie |
|---|---|
| Zakręty | szybkość w zakrętach, czyli „czyste tempo”, ale nie jako jedna liczba, która rządzi wszystkim |
| Hamowanie | dohamowania, stabilność i skuteczność na wejściu w zakręt |
| Płynność | delikatność dla auta: opony, hamulce, skrzynia. **Wartość jest stała, a jej waga zależy od epoki**, np. w latach 50. płynność ratuje skrzynię, dziś głównie opony. Dzięki temu cecha jest uczciwa w każdej erze. |
| Wyprzedzanie | skuteczność ataku |
| Obrona | utrzymanie pozycji |
| Regularność | powtarzalność czasów, mało błędów |
| Opanowanie | presja, starty, walka o tytuł, skupienie |
| Adaptacja | szybkość wejścia w nowe auto, nowy tor i zmienne warunki |
| Deszcz | mokry i przesychający tor |
| Kondycja | utrzymanie formy w długim, gorącym albo fizycznie ciężkim wyścigu (lata 50., brak wspomagania, endurance) |
| Informacja zwrotna | rozwój auta, ustawienia, zrozumienie części |

**Ocena ogólna** to ważona suma atrybutów, której wagi zależą od epoki.

**Doświadczenie nie jest atrybutem, tylko licznikami z konkretnym działaniem:**
- starty ogółem: mniej błędów w chaosie (restarty, kolizje w pierwszym okrążeniu);
- okrążenia na danym torze: znajomość toru;
- wyścigi w deszczu: mniejsza kara za pierwsze mokre wyścigi;
- sezony w zespole: lepsza współpraca z inżynierami.

**Preferencje prowadzenia:** balans (podsterowność ↔ nadsterowność), trakcja (miękka ↔ ostra) i **styl hamowania (wczesne / normalne / późne)**. Porównywane z charakterystyką auta (§5.2).

**Osobowość:**
- *Jedna główna:* szuka bezpieczeństwa, najemnik, lojalny, prestiżowy, krótkoterminowy, ambitny, mentor, gracz zespołowy.
- *Ukryte wartości 1–20:* lojalność, ambicja, temperament, profesjonalizm, ego.

Każda z nich zmienia wagi oceny oferty (§8) oraz reakcje na status #2, na złamane obietnice i na złe wyniki. Poznaje się je z czasem: z rozmów, z historii kariery i dzięki relacjom.

**Cechy (0–3):** np. mistrz kwalifikacji, zaklinacz opon / niszczyciel opon, szybki tylko w czystym powietrzu, artysta wyprzedzania, mistrz deszczu, pękający pod presją, skłonny do kraks, „mechanik” (oszczędza sprzęt), kierowca z pieniędzmi, mentor (rozwija partnera z zespołu). Widoczne dopiero po obserwacji.

**Stan bieżący (zmienny):** forma (średnia krocząca), morale, pewność siebie (spada po błędach i w aucie, które mu nie leży), zdrowie i kontuzje. Zmęczenia nie ma w widoku składu. Istnieje tylko wewnątrz długich wyścigów endurance (stinty), gdzie liczy się kondycja.

**Tory:** trzy warstwy, od najbardziej wytłumaczalnej:
1. **Z atrybutów × profil toru:** wynika wprost z liczb. Na przykład w Monako liczą się regularność i opanowanie, a na Monzy więcej daje walka na prostych.
2. **Znajomość toru:** rośnie z przejechanymi okrążeniami. Nowy tor albo nowa wersja układu to strata dla wszystkich, mniejsza dla kierowców o wysokim doświadczeniu.
3. **Ukryte powinowactwo (±, małe):** niektórzy kierowcy są po prostu lepsi albo gorsi na konkretnych torach, czego atrybuty nie tłumaczą. W trybie historycznym powinowactwo wylicza pipeline z prawdziwych wyników: to, co zostaje po odjęciu modelu (Senna w Monako, Hamilton na Silverstone). Kierowcy generowani losują je. Gracz odkrywa je z czasem, np. inżynier mówi: „on ma coś z tym torem”.

Do tego wyścig domowy: przewaga morale, ale też presja, zależnie od opanowania.

**Kariera i statystyki:** starty, zwycięstwa, pole position, podia, punkty, tytuły, najszybsze okrążenia, nieukończone wyścigi (z podziałem na własne błędy i awarie), historia sezon po sezonie i zespół po zespole, pojedynki z partnerami z zespołu, rekordy, historia kontuzji.

**Tryb porównania:** dowolnych dwóch (albo więcej) kierowców, a w nim:
- całe kariery;
- wybrane sezony obok siebie (np. Senna 1988 kontra Verstappen 2023);
- pojedynki bezpośrednie (kwalifikacje, wyścigi, wspólne lata w zespole);
- statystyki względne wobec auta.

Porównania między epokami są opatrzone kontekstem: liczba wyścigów, punktacja i awaryjność epoki.

**Kontrakt, a nie „wartość rynkowa”.** To nie piłka nożna, kierowców rzadko się wykupuje. Profil pokazuje:
- wartość bieżącego kontraktu, lata do końca, premie;
- **klauzule** (§10);
- oczekiwania co do następnej umowy;
- sławę, czyli popularność u kibiców, która napędza sponsorów i model popularności (§9).

**Ukryte (tylko Spy albo pasma ze scoutingu):** potencjał, podatność na kontuzje, powinowactwo do torów.

Personel ma analogiczny profil: tożsamość, atrybuty roli, osobowość, stan, historia kariery i relacje.

### 6.2. Personel (PROPOZYCJA do ustalenia; PP-033)

**Dwa poziomy: kluczowi ludzie i działy.** W zespole nie pracuje 10 osób, tylko od ~20 (lata 50.) do ~1000+ (dziś).
- **Kluczowi ludzie** (tabela niżej) to imienni specjaliści z pełnym profilem. Kierują działami i podejmują decyzje.
- **Działy:** projektowy, aerodynamiki, silnika, produkcji, dynamiki pojazdu, zespół wyścigowy i mechanicy, komercyjny, scouting. Każdy ma:
  - **liczebność** (zatrudniasz i zwalniasz ludzi grupami),
  - **średnią jakość** (poziom rekrutacji i szkolenia),
  - **koszt na osobę**.
- **Liczebność decyduje o tempie, jakość o wyniku:** więcej ludzi to szybciej ukończone projekty (z malejącymi korzyściami), a jakość efektu zależy od kluczowych ludzi, średniego poziomu działu, infrastruktury i budżetu. Każdy kolejny człowiek daje mniej, a zbyt duży dział traci na koordynacji. Nie da się wygrać samym zatrudnianiem.
- **Zatrudnianie trwa** (rekrutacja, wdrożenie), a zwolnienia obniżają morale. W erze limitu wydatków liczebność ogranicza budżet.
- **Talenty z wnętrza działów:** co jakiś czas w dziale wyrasta ktoś wybitny, kto może awansować na kluczowe stanowisko. W trybie historycznym prawdziwi inżynierowie zaczynają właśnie tak (Adrian Newey zaczynał jako młody inżynier w Fittipaldi w 1980).

**Kluczowi ludzie:** każda rola ma 3–4 własne atrybuty, a nie wspólną listę. Wszyscy mają też doświadczenie, osobowość (ambicja, lojalność) i krzywą wieku. Role pojawiają się razem z epoką.

| Rola | Od | Atrybuty | Na co wpływa |
|---|---|---|---|
| Dyrektor techniczny | 1950 | wizja, zarządzanie projektem, innowacyjność | koncepcja auta, ryzyko rewolucji, praca działu technicznego |
| Główny projektant | 1950 | podwozie, integracja, precyzja | jakość wykonania koncepcji |
| Projektant silników | 1950 (jeśli budujesz silniki) | moc, niezawodność, wydajność | silnik |
| Szef aerodynamiki | ~1968 | aerodynamika, korelacja tunel–tor, innowacyjność | docisk, trafność rozwoju |
| Szef dynamiki pojazdu | 1950 | zawieszenie, opony, temperatura opon | przyczepność mechaniczna, zużycie opon |
| Inżynier wyścigowy (1 na kierowcę) | ~1970 | ustawienia, relacja z kierowcą, analiza danych | tempo w weekendzie, forma kierowcy |
| Strateg | ~1994 (tankowanie) | strategia, reakcja, pogoda | decyzje w wyścigu |
| Szef mechaników | 1950 | pit-stopy, jakość montażu, organizacja | czas postojów, awarie |
| Skaut | 1950 | ocena talentu, sieć kontaktów | zawężanie pasm w puli talentów |
| Dyrektor komercyjny | ~1968 (sponsorzy) | negocjacje, marketing, sieć | sponsorzy, przychody |

Morale personelu (np. po zwolnieniu kolegi, przy słabych wynikach, przy dużym budżecie) lekko przesuwa jego skuteczność. Szef zespołu (gracz albo AI) ma atrybuty menedżerskie: negocjacje, zarządzanie ludźmi, polityka (wpływ na regulamin) i biznes.

### 6.3. Zaufanie i obietnice (PP-035)
„Relacje” to w praktyce **pamięć i zaufanie** między ludźmi i zespołami (0–100, z historią zdarzeń): dotrzymane lub złamane obietnice, wspólne sukcesy, kraksy między partnerami z zespołu, spory kontraktowe, mentorstwo.

**System obietnic i warunków:**
- **Kierowca stawia warunki**, np. „podpiszę jeszcze jeden rok, ale to ostatnia szansa: chcę widzieć podia” albo „chcę statusu #1”, „chcę lepszego silnika”.
- **Zespół składa obietnice**, np. nowy tunel aerodynamiczny, status, konkretny partner z zespołu, auto zbudowane pod jego styl.
- Każda obietnica ma **termin i mierzalny warunek**. Gra ją śledzi i pokazuje w profilu (STATE / WHY / FORECAST).
- **Dotrzymanie buduje zaufanie**, a złamanie je niszczy. Kierowca lojalny wybacza raz, najemnik odchodzi od razu. Niskie zaufanie oznacza twardsze negocjacje, odejście przy pierwszej okazji i gorszą atmosferę.

Z zaufania wyrastają też rywalizacje (Senna–Prost), napięcia między partnerami z zespołu i historie w skrzynce, bez skryptu.

### 6.4. Rozwój i wiek
Krzywa kariery obejmuje wzrost, szczyt, plateau i spadek, z indywidualnymi datami. W trybie „Trajektoria” zastępuje ją prawdziwa krzywa.

---

## 7. Wyścig

**Automatyczny, sterowany przez sztab (zostaje ze starej dokumentacji, PP-029).** Nie ma magazynu pojedynczych części ani mikrozarządzania: zatrudniasz stratega, a on steruje postojami i tempem. Gra jest modularna, więc chętni mogą włączyć moduły ręcznej kontroli (np. ręczne pit-stopy i polecenia tempa w wyścigu). To ustawienie kariery, a nie wymóg. Gracz przygotowuje zespół, a strategię w wyścigu realizują jego ludzie. Jakość ich decyzji zależy od ich umiejętności. Race Spy wyjaśnia każdą decyzję.

**Czas okrążenia składa się z warstw:** baza toru, dopasowanie samochodu, kierowca, paliwo, opony, ruch i brudne powietrze, pogoda, szum losowy. **Każda warstwa jest parametryzowana epoką:**
- **lata 50.:** jedna mieszanka opon, rzadkie postoje, awaryjność decyduje o połowie wyników, zmiana kierowcy w trakcie wyścigu;
- **era tankowania:** strategia paliwowa;
- **współczesność:** mieszanki opon, obowiązkowy postój, samochód bezpieczeństwa i VSC, DRS od 2011.

**Kwalifikacje** mają format epoki.
**Incydenty i awarie:** ryzyko zależy od epoki (bezpieczeństwo), kierowcy (agresja, opanowanie) i sprzętu. Skutki to kontuzje i końce karier. Śmierć tylko przy włączonej opcji (PP-006).
**Prezentacja:** najpierw backend (PP-020). Wyścig ma wynik natychmiastowy albo relację na żywo w formie tabeli z różnicami czasu i komunikatami. Mapa toru 2D powstanie po zbudowaniu UI.

---

## 8. AI szefów zespołów

- **Strategia należy do osoby (szefa), a nie do zespołu** (wzorzec z Ping-Pong Managera). Zwolniony szef zostaje zastąpiony innym, a zespół widocznie zmienia kierunek.
- **Archetypy:** *Pretendent* (wszystko na teraz), *Budowniczy* (długi plan, infrastruktura), *Oportunista* (celuje w wybrane tory i okazje), *Przetrwanie* (budżet, kierowcy z pieniędzmi).
- **Decyzje wyborem największej użyteczności.** Ocena oferty przez kierowcę:
  `U = w1·prestiż + w2·przewidywany samochód + w3·pensja względem wartości rynkowej + w4·status (#1/równy/#2) − w5·ryzyko zespołu`, z wagami zależnymi od osobowości.
- **Poświęcenie sezonu:** AI przenosi zasoby na przyszły samochód, gdy jego pozycja jest ustabilizowana albo nadchodzi duża zmiana przepisów.
- **Bez wszechwiedzy** (D-010): AI korzysta ze scoutingu i obserwacji, a nie z ukrytych atrybutów ani ze znajomości przyszłości.

---

## 9. Ekonomia i popularność (PP-025)

### 9.1. Popularność sportu napędza pieniądze
Pieniądze w sporcie rosną (albo spadają) **z popularności, a nie z automatycznej inflacji**. Model popularności:
- **Globalna popularność serii**, plus **baza fanów w każdym kraju**.
- **Co ją podnosi:** wyrównane walki o tytuł (wielu pretendentów, rozstrzygnięcia w ostatnich wyścigach), gwiazdy i ich rywalizacje, krajowi bohaterowie (np. mistrz z Polski buduje polski rynek), wyścigi w nowych krajach, era TV.
- **Co ją obniża:** długa dominacja jednego zespołu lub kierowcy, nudne wyścigi, skandale, (przy włączonej opcji) tragedie.
- **Tryb historyczny:** bazowy wzrost pochodzi z osi czasu epok (np. era TV, umowa Concorde), a popularność świata go wzmacnia lub osłabia. Po 2026 działa już tylko model.

**Skutki:**
- pula pieniędzy z TV i nagród rośnie lub maleje;
- rynek sponsorów w każdym kraju się zmienia;
- **pensje rosną naturalnie**, bo zespoły mają więcej pieniędzy, a gwiazdy są więcej warte (to jest „inflacja zarobków”);
- rośnie popyt na nowe wyścigi w krajach z dużą bazą fanów.

### 9.2. Przychody i koszty
- **Przychody według epoki:** pieniądze startowe → sponsorzy (od 1968) → TV i umowa Concorde → nagrody za pozycję w konstruktorach, sprzedaż silników i samochodów klienckich.
- **Koszty:** pensje, projekt i budowa aut, silniki, podróże, infrastruktura (także jej starzenie się, §4.3), naprawy po wypadkach.
- **Sponsorzy** to rynek z celami, a jego wielkość w każdym kraju wynika z popularności. Sponsorzy mają **branże** zmieniające się z epokami: paliwa i opony w latach 50., tytoń od 1968 do zakazów w latach 2000., alkohol, banki, telekomy, IT, kryptowaluty. Zakaz reklamy tytoniu to w trybie historycznym realny szok finansowy dla zespołów od niej zależnych.
  - **Miejsca na aucie:** główne, drugorzędne, mniejsze. Każde daje inną część kwoty.
  - **Sponsor tytularny zmienia nazwę zespołu** (historycznie np. „Marlboro McLaren”, „Mild Seven Benetton”). Wymaga prestiżu i długiej umowy, może też chcieć udziałów.
  - **Rozmowy przyspieszają:** zgodna narodowość sponsora i kierowcy, prestiż zespołu, przedłużenie istniejącej umowy.
  - **Rozmowy spowalniają:** konflikt branż, bo dwa koncerny paliwowe na jednym aucie nie przejdą.
- **Gotówka to nie budżet:** UI rozróżnia gotówkę, zobowiązania, pewne przychody i prognozę. Jeśli czegoś nie da się kupić, gra mówi dokładnie dlaczego.
- **Kwoty nominalne:** UI pokazuje prawdziwe kwoty, które z biegiem lat rosną razem ze sportem.

---

## 10. Rynek i kontrakty

- **Pola kontraktu:** pensja, premie (za punkty, zwycięstwa, tytuł), długość, status (#1 / równy / #2 / rezerwowy). Kierowcy wnoszący sponsora mają osobne pole.
- **Klauzule** (głęboki system, którego nikt w gatunku nie ma):
  - **wyjścia z umowy:** kierowca może odejść, jeśli zespół spadnie poniżej np. 5. miejsca w konstruktorach albo nie da mu podium w ciągu N wyścigów; zespół może rozwiązać umowę, jeśli kierowca przegra pojedynek z partnerem albo nie zdobędzie punktów;
  - **opcja przedłużenia** po stronie zespołu albo kierowcy;
  - **gwarancje:** status #1, partner z zespołu (albo zakaz konkretnego), liczba samochodów zespołu, dostęp do nowych części jako pierwszy;
  - **pieniądze:** premie za wyniki, udział w dochodach sponsora osobistego, kara za zerwanie;
  - **zakazy:** udział w innych seriach (np. Le Mans), ryzykowne hobby;
  - **zwolnienie za kwotę:** rzadkie, ale możliwe (historycznie wykupy kontraktów się zdarzały).
- Każdą klauzulę można negocjować. Obie strony wyceniają ją według swojej osobowości i sytuacji. Złamanie klauzuli jest sprawą w skrzynce i uderza w zaufanie (§6.3).
- **Negocjacje z ludźmi, a nie z paskami:** UI pokazuje powody odmowy lub zgody.
  - **Ograniczona liczba prób:** zwykle kilka, u niecierpliwych mniej. Kolejne drobne podbijanie pensji o grosze irytuje drugą stronę i obniża jej zainteresowanie. Liczy się realna zmiana oferty (status, klauzula, lata), a nie klikanie suwaka.
  - **Rywale składają oferty równolegle.** Kierowca porównuje wszystkie akceptowalne oferty, a przy remisie przesądza zaufanie (§6.3). Obecny zespół nie wygrywa automatycznie.
  - **Kierowcy i personel sami zgłaszają się z propozycjami** („jestem zainteresowany, oto moje warunki”).
- **Umowy handlowe (sponsorzy, dostawcy) prowadzi dział komercyjny.** Jego liczebność i jakość decydują o tym, ile rozmów prowadzisz naraz i jak szybko idą. **Im dłużej negocjujesz, tym lepsze warunki możesz wynegocjować, ale rywal może w tym czasie podpisać umowę przed Tobą.** Pytanie „podpisać teraz czy czekać” jest świadomym ryzykiem, a gra pokazuje, kto jeszcze rozmawia z tym partnerem (o ile to wiesz).
- **Dynamika okna transferowego:** negocjacje trwają tygodnie. Liczba prowadzonych naraz rozmów zależy od ludzi i infrastruktury, a im później w sezonie, tym mniej wolnych miejsc i krótsze okna decyzji. Kierowca bez miejsca pod koniec sezonu obniża oczekiwania. Spóźnienie na rynek kosztuje stanem rynku, a nie zablokowanym przyciskiem.
- **Scouting:** obcy zawodnicy mają atrybuty w pasmach (np. 12–16), które zawężają się dzięki obserwacji, wspólnym startom albo podpisaniu kontraktu.

---

## 11. Czas i skrzynka

- **Postęp dzień po dniu** (PP-016). Przycisk „Dalej” przewija dni do najbliższej sprawy wymagającej uwagi. W dzień wyścigu zmienia się w „Weekend wyścigowy”.
- **Skrzynka:** sprawy przychodzą tylko wtedy, gdy mają treść. Cisza jest OK. Tylko decyzja blokuje przewijanie czasu. Skutek każdej opcji widać przed kliknięciem (wzorzec z Ping-Ponga).
- **Zdarzenia życiowe** (kontuzja, spadek formy, konflikt w zespole, mentor, rodzina) wywodzą się ze stanu osoby, a nie z losowego generatora wypełniaczy.

---

## 12. Żywa historia

- **Kronika rozbieżności:** tytuły twoje i prawdziwe; zespoły, które nie powstały albo przetrwały dłużej; technologie wprowadzone wcześniej lub później; kariery, które się nie wydarzyły. Dostępna jako ekran z osią czasu i jako komentarze w skrzynce („W prawdziwej historii Clark zdobyłby dziś swój pierwszy tytuł”).
- **Hall of Fame, rekordy, historia sezonów:** w kompaktowej formie (TECH §6).

---

## 13. Tryb bez liczb (PP-031)

Opcja kariery: **grasz bez atrybutów, ocen i statystyk osiągów.** Wszystko, co wiesz o kierowcach, aucie i rywalach, pochodzi z **opinii Twoich ludzi**:
- „Jesteśmy mocni w wolnych zakrętach, na prostych tracimy do Ferrari mniej więcej 3 dziesiąte.”
- „Młody jest bardzo szybki, ale zjada opony.”
- „Nie ufam temu pakietowi, dajcie nam jeszcze jeden test.”

To nie jest osobny system, tylko **inna prezentacja tej samej wiedzy** (INV-003): gra zawsze pokazuje wiedzę zespołu, a nie prawdę, raz jako pasma liczb, raz jako słowa. **Trafność opinii zależy od jakości ludzi.** Słaby inżynier potrafi się mylić, mieć w czymś przesadną pewność albo coś przeoczyć. Wtedy dobór personelu staje się jeszcze ważniejszy. Wyniki, czasy okrążeń i tabele mistrzostw są widoczne zawsze, bo to fakty publiczne.

---

## 14. Później (faza 7+)

Serie juniorskie jako prawdziwa ścieżka rozwoju, Le Mans / WEC / GT (drabina klas, zespoły z kilkoma programami, realistyczne zasady wejścia na wyścigi 24h), tryb proceduralny od zera, tryb wyzwań, edytor bazy.
