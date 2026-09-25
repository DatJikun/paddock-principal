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

Pełną oś czasu budujemy w fazie 1 razem z danymi.

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

Technologia wymaga badań (personel i pieniądze), ma ryzyko porażki i szansę, że rywale ją skopiują. **Zakazy reagują na świat:** propozycja zakazu pojawia się, gdy dana technologia zbyt mocno dominuje w *Twoim* świecie, a nie w tym prawdziwym roku.

---

## 5. Samochód

- **Projekt roczny:** koncepcja plus wybrane technologie, tworzone przez głównego projektanta. Jakość zależy od personelu, budżetu i infrastruktury.
- **Rozwój w sezonie:** małe przyrosty. Decyzja o „poświęceniu sezonu” (wcześniejsze przejście na nowy samochód) jest dostępna dla gracza i dla AI.
- **Wektor osiągów:** Moc, Docisk (ograniczony epoką i technologią), Przyczepność mechaniczna, Hamowanie, Niezawodność.
- **Silnik:** kliencki (umowa z dostawcą epoki, np. Coventry-Climax, Cosworth DFV) albo **własny** (PP-019). Gracz może zostać producentem silników, jak Ferrari, BRM czy Honda:
  - wymaga to projektanta silników, osobnego budżetu i czasu (pierwszy silnik powstaje co najmniej sezon);
  - własny silnik można **sprzedawać zespołom klienckim**, co daje przychód, wpływy i dane, ale też tworzy rywali na tym samym sprzęcie;
  - przepisy epoki (pojemność, turbo, hybryda) mogą unieważnić projekt, więc zmiana regulaminu to realne ryzyko.
- **Opony:** dostawca z epoki, z wojnami oponiarskimi (Goodyear–Firestone, Michelin–Bridgestone).
- **Dopasowanie do toru:** iloczyn skalarny wektora samochodu i wag profilu toru (prosta, szybkie zakręty, wolne zakręty, hamowanie). Ten mechanizm zostaje ze starej dokumentacji.

---

## 6. Ludzie

### 6.1. Kierowca (UI w skali 1–20)
| Atrybut | Znaczenie |
|---|---|
| Tempo | czysta prędkość na okrążeniu |
| Walka | wyprzedzanie i obrona (zamiast osobnych Attack/Defend) |
| Regularność | powtarzalność czasów, mniej błędów |
| Deszcz | jazda w mokrych warunkach |
| Oszczędzanie sprzętu | opony, hamulce, skrzynia; w latach 50. równie ważne jak tempo |
| Opanowanie | zachowanie pod presją, starty |
| Informacja zwrotna | wpływ na rozwój samochodu i ustawienia |
| Doświadczenie | rośnie z każdym startem |

**Osobowość:** ambicja, lojalność, temperament, skłonność do ryzyka. Wpływa na kontrakty, relacje w zespole i ryzyko wypadku.
**Ukryte:** potencjał, podatność na kontuzje. Widoczne tylko w Spy albo jako pasma ze scoutingu.

### 6.2. Personel
Role pojawiają się razem z epoką:
- od początku: szef zespołu, główny projektant, szef mechaników, opcjonalnie projektant silnika;
- od końca lat 60.: aerodynamik;
- od lat 70.–80.: inżynier wyścigowy;
- od lat 90.: strateg.

Każda rola ma 2–4 atrybuty istotne dla swojej pracy, a nie wspólną listę dla wszystkich.

### 6.3. Rozwój i wiek
Krzywa kariery obejmuje wzrost, szczyt, plateau i spadek, z indywidualnymi datami. W trybie „Trajektoria” zastępuje ją prawdziwa krzywa.

---

## 7. Wyścig

**Automatyczny, sterowany przez sztab (zostaje ze starej dokumentacji).** Gracz przygotowuje zespół, a strategię w wyścigu realizują jego ludzie. Jakość ich decyzji zależy od ich umiejętności. Race Spy wyjaśnia każdą decyzję.

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

## 9. Ekonomia

- **Pieniądz w indeksie epoki** (PP-010): wartości w jednostce znormalizowanej, UI może pokazać kwotę nominalną.
- **Przychody według epoki:** pieniądze startowe → sponsorzy → pieniądze z TV i nagrody z umowy Concorde → nagrody za pozycję w konstruktorach.
- **Koszty:** pensje, projekt i budowa samochodów, silniki klienckie, podróże, infrastruktura, naprawy po wypadkach.
- **Sponsorzy** to rynek z celami, a nie stała kwota (za Pelotonem, D-011).
- **Gotówka to nie budżet:** UI rozróżnia gotówkę, zobowiązania, pewne przychody i prognozę. Jeśli czegoś nie da się kupić, gra mówi dokładnie dlaczego.

---

## 10. Rynek i kontrakty

- **Pola kontraktu:** pensja, premie (za punkty i zwycięstwa), długość, status (#1 / równy / #2 / rezerwowy), klauzula wykupu. Kierowcy wnoszący sponsora mają osobne pole.
- **Negocjacje z ludźmi, a nie z paskami:** UI pokazuje powody odmowy lub zgody.
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

## 13. Później (faza 7+)

Serie juniorskie jako prawdziwa ścieżka rozwoju, Le Mans / WEC / GT (drabina klas, zespoły z kilkoma programami, realistyczne zasady wejścia na wyścigi 24h), tryb proceduralny od zera, tryb wyzwań, edytor bazy.
