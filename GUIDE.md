# Jak działa gra

Przewodnik dla osób, które testują grę. Pokazuje, co w grze wybierasz i jak te wybory działają. Na końcu każdego rozdziału są pytania, na które chcemy znać Twoją odpowiedź.

```pola
Po co to czytasz | żeby ocenić, jak te wybory działają w grze | każdy rozdział kończy się pytaniami
Liczby | to wstępne ustawienia | mogą się zmienić po Waszych uwagach
Jak odpowiedzieć | napisz, co czujesz | np. „sponsorzy podpisują z rywalami za szybko”, bez żadnych liczb
```

---

## 1. Start kariery

Z menu głównego wybierasz Kontynuuj (ostatni zapis), Nową karierę, Szybki wyścig, Wczytaj, Ustawienia albo Wyjdź. Nowa kariera to cztery kroki: Ty, Świat, Zespół i podsumowanie, w którym dopiero „Rozpocznij” startuje grę. W trakcie gry menu otwiera klawisz Esc: Zapisz, Zapisz jako, Wczytaj, Ustawienia i Wyjdź do menu. Gra zapisuje się tylko wtedy, gdy sam o to poprosisz.

Przycisk Dalej sam przesuwa kolejne dni, dopóki nic nie wymaga Twojej uwagi. Zatrzymuje się, gdy czas trzyma nierozstrzygnięta decyzja, gdy nadchodzi dzień wyścigu (wyścig uruchamiasz kolejnym kliknięciem), po wyścigu i po przełomie sezonu, oraz gdy w Skrzynce pojawi się nowa ważna wiadomość (wtedy dostajesz też powiadomienie w prawym dolnym rogu). W trakcie biegu Dalej zmienia się w Pauzę (albo Esc). W Ustawieniach wybierasz, czy Dalej biegnie sam i ile czasu zajmuje jeden dzień (domyślnie pół sekundy, do wyboru cztery tempa).

```wybory
Ty | imię, nazwisko, narodowość (każda z danych o ludziach) i jedna cecha szefa, w której jesteś mocniejszy; przy każdej opcji jest jej krótki opis
Rok i zespół | pierwszy sezon do testów to 1955, zespół wybierasz z kart
Karta zespołu | skład kierowców, silnik (fabryczny albo nazwa dostawcy), budżet w dolarach, miejsce w poprzednim sezonie (gdy jest znane), cztery poziomy od 1 do 5 (auto, infrastruktura, kierowcy, personel) i znak zespołu; karty idą w kolejności poprzedniego sezonu
Preset | najbardziej historyczny, zbalansowany albo chaos
Ludzie | prawdziwa kariera rok po roku, prawdziwy sufit talentu, prawdziwe nazwiska z losowymi umiejętnościami albo wszyscy generowani
Przepisy | historyczne albo głosowane co sezon; przy głosowanych wybierasz jeden głos na zespół albo bank głosów (rozdział 14)
Zachowanie AI | odtwarza historię, reaguje na sytuację albo gra losowo
Siła historii | skala 0–10: jak chętnie AI powtarza prawdziwe zdarzenia
Losowość | suwak 0–100: rozwój, forma, awarie
Śmiertelność | domyślnie wyłączona
```

```pola
Najbardziej historyczny | kierowcy jadą swoją prawdziwą krzywą | historyczne przepisy, AI odtwarza historię
Zbalansowany | kierowcy mają prawdziwy sufit talentu | historyczne przepisy, AI reaguje na sytuację; ustawienie domyślne
Chaos | dowolna kombinacja osi | aż po w pełni generowany świat
Stawka | 2 auta i 2 stałych kierowców na zespół | bez przesiadek w sezonie
```

```porownanie Prawdziwa kariera | Prawdziwy potencjał
Kierowca jest tak dobry | jak naprawdę w danym roku | jak pozwoli mu Twój świat
Prawdziwa kariera to | krzywa rok po roku | sufit talentu
```

```pytania
Który preset wybrałbyś na pierwszą kampanię i dlaczego?
Czy wolisz, żeby kierowca był tak dobry, jak naprawdę był w danym roku, czy żeby mógł rozwinąć się inaczej?
Czy suwaki (siła historii, losowość) są zrozumiałe, czy wystarczą same presety?
```

---

## 2. Kierowcy i wiek

Kierowca ma 11 atrybutów, gwiazdki i wiek. Wiek decyduje, kiedy osiągnie szczyt i kiedy zacznie słabnąć.

```wybory
Kogo zatrudnić | patrzysz na gwiazdki, atrybuty i doświadczenie; cudzych kierowców widzisz tylko w widełkach, np. 12–16
Młody talent czy doświadczony kierowca | młody dopiero rośnie, starszy może być już po szczycie
Kiedy się rozstać | po szczycie kierowca traci poziom co roku
Kierowca do auta | styl kierowcy musi pasować do auta (rozdział 6)
```

```pola
Atrybuty | skala 1–20 | zakręty, hamowanie, płynność, wyprzedzanie, obrona, regularność, opanowanie, adaptacja, deszcz, kondycja, informacja zwrotna
Gwiazdki | średnia atrybutów ÷ 4 | 20 = 5★, 10 = 2,5★
Ocena ogólna | 1–100, średnia ważona | wagi zależą od epoki
Doświadczenie | starty, okrążenia na torze, wyścigi w deszczu, sezony w zespole | to liczniki, nie atrybut
```

```kroki
Rozwój | od {RatingsCareerArc.YearsOfGrowthBeforeDebut|lat} przed debiutem w F1
Szczyt | {RatingsCareerArc.MinPeakAge}–{RatingsCareerArc.MaxPeakAge} lat, z najlepszego sezonu
Spadek | od {RatingsCareerArc.DeclineStartMin}–{RatingsCareerArc.DeclineStartMax} lat, {RatingsCareerArc.DeclineLevelsPerYear|pkt} poziomu na rok
Atrybuty | kondycja spada pierwsza, opanowanie, regularność i informacja zwrotna rosną najdłużej
Emerytura | prawdziwy kierowca po swoim ostatnim prawdziwym sezonie, pozostali losowo od {CareerDayEstimates.DriverRetirementFromAge|lat}, na pewno w wieku {CareerDayEstimates.DriverRetirementCertainAge|lat}
```

```wykres luk-kariery
Kto odszedł na szczycie (Fangio), słabnie dopiero po ostatnim prawdziwym sezonie.
```

```wykres emerytura
```

```pytania
Czy gwiazdki wystarczają do decyzji, czy chcesz widzieć atrybuty?
Czy wiek, w którym kierowca zaczyna słabnąć, pasuje do tego, co wiesz o prawdziwych karierach?
Czy zdarzyło się, że 40-latek wygrywał seryjnie albo 23-letni talent stał w miejscu?
```

---

## 3. Oceny prawdziwych kierowców

Każdy kierowca wchodzi do gry z dwiema liczbami: aktualną oceną i maksymalnym potencjałem. Na razie to wartości wstępne, dokładniejsze ustalimy później.

```pola
Ocena na wejściu | jak dobry jest kierowca, gdy pojawia się w grze | gwiazdki 0–5
Maksymalny potencjał | sufit, do którego może dojść | zależy od świata: auta, ludzi, szczęścia
Skąd liczby | wyniki wyścigów od 1950, względem stawki jego czasów | wstępne, do poprawki
```

```wykres wagi-oceny
W latach 50. płynność i kondycja ratowały wyścig, dziś więcej daje regularność i informacja zwrotna.
```

```pytania
Czy różnica między oceną a potencjałem młodych kierowców wydaje Ci się sensowna?
```

---

## 4. Juniorzy i skauci

Nowi ludzie wchodzą do gry przez pulę talentów, czyli świat poza F1: juniorzy, kierowcy z innych serii i testerzy. Juniorzy są na rynku, widocznym dla wszystkich zespołów. Skauci pomagają odgadnąć, kto z nich wyrośnie, a Ty możesz przyjąć najciekawszych do własnej akademii, osobnej i z ograniczoną liczbą miejsc.

```wybory
Kogo przyjąć do akademii | akademia ma {PoolEstimates.AcademySlots|osób} miejsca; przyjęty junior jest tylko Twój: znika z rynku dla innych zespołów i nikt inny go nie podpisze; miejsce możesz zwolnić, a junior wraca na rynek
Który program mu opłacić | podstawowy przyspiesza jego rozwój do {PoolEstimates.CheapSlowSpeedPercent|%%} zwykłego tempa i kosztuje {PoolEstimates.CheapSlowCostShare|%} typowego budżetu na sezon, intensywny do {PoolEstimates.ExpensiveFastSpeedPercent|%%} za {PoolEstimates.ExpensiveFastCostShare|%}; program zmienia tylko tempo, nigdy sufit talentu
Obserwuj całą pulę | wolno, wszyscy naraz
Obserwuj jedną osobę | szybko, jej widełki się zawężają
Podpisz | kierowca wyścigowy, testowy albo junior z opcją; juniora innej akademii podpisać nie można
```

```pola
Rynek juniorów | widzisz wszystkich, których nikt nie przyjął do akademii, z widełkami z własnego skautingu | prawdziwych i fikcyjnych nie da się odróżnić
Prawdziwi kierowcy | trafiają do puli {PeopleScheduleRules.DefaultPoolLeadYears|lat} przed debiutem | najwcześniej w wieku {PeopleScheduleRules.PoolMinimumAgeYears|lat}
Fikcyjni juniorzy | pula uzupełniana do {PoolEstimates.TargetSize|osób} co sezon | AI nie wie, kto jest prawdziwy
Odpada | po {PoolEstimates.MaxSeasonsInPool|sezonów} bez kontraktu albo po {PoolEstimates.MaxAge|lat}; junior z Twojej akademii, który odpada, zostawia wiadomość w skrzynce
Rozwój | {PoolEstimates.DevelopmentRatePercent|%%} dystansu do potencjału na sezon | najwyżej {PoolEstimates.MaxAnnualStep|pkt} na atrybut
```

```wykres pula-rozwoj
Program przyspiesza rozwój, ale nie podnosi sufitu.
```

```wykres pasmo-skauta
Widełki potencjału są szersze niż widełki atrybutu. Słaby skaut może się mylić.
```

```pytania
Czy wybór między obserwacją całej puli a jednej osoby to dla Ciebie prawdziwa decyzja?
Czy ograniczone miejsca w akademii i to, że przyjęty junior znika innym z rynku, zmieniają to, kogo wybierasz?
Czy junior z programem jest gotowy w sensownym czasie, a przyspieszenie jest odczuwalne, choć skromne?
Czy niepewność skauta pomaga w decyzji, czy tylko irytuje?
```

---

## 5. Kontrakty

Kierowca ocenia ofertę według swojej osobowości. Jednej „wartości rynkowej” nie ma.

```wybory
Pensja | porównywana z typową pensją tego poziomu w epoce
Długość | do {NegotiationEstimates.MaxYears|sezonów}; długość, opcje i klauzula wyjścia liczą się jako ryzyko
Status | numer 1, równy albo numer 2
Kiedy ruszyć kontrakt rywala | na {NegotiationEstimates.NegotiationWindowDays|dni} przed jego końcem
Czy dociskać | podwyżka poniżej {NegotiationEstimates.MinMeaningfulImprovementPercent|%%} nie jest zmianą i odbiera zainteresowanie
Zerwać umowę | płacisz {NegotiationEstimates.TerminationShare|%} reszty pensji
Koniec umowy | pół roku wcześniej dostajesz pytanie o przedłużenie; kierowcy i kluczowy personel (dyrektor techniczny, główny konstruktor) mają je osobno, reszta personelu jedną wspólną skrzynką
Bez odpowiedzi | po {NegotiationEstimates.RenewalDecisionDays|dni} umowa po prostu kończy się w swoim terminie i dostajesz o tym wiadomość; nic nie przedłuża się samo. Przedłużenie na obecnych warunkach wymaga Twojego wyboru i zgody osoby; budżet nie jest warunkiem, o pieniądzach ocenia zarząd. Czas przez te pytania nie stoi
Własna umowa szefa zespołu | nie wygasa i nie ma o niej pytań; jesteś po prostu w zespole
Gdy rywal kusi Twojego kierowcę | w ostatnim roku umowy; zależnie od morale i lojalności kierowca odchodzi, prosi o lepsze warunki albo zostaje
Podwyżka w trakcie umowy | co najwyżej raz w sezonie | odmowa zabiera {NegotiationEstimates.RaiseTrustHit} zaufania
Gdy kierowca prosi o podwyżkę | w trakcie umowy; kierowca na szczycie prosi o mniej niż wschodząca gwiazda
```

```pola
Prestiż zespołu | waga {NegotiationEstimates.WeightPrestige}
Przewidywane auto | waga {NegotiationEstimates.WeightCar} | punkty, wygrane, szansa na tytuł
Pensja | waga {NegotiationEstimates.WeightSalary}
Status | waga {NegotiationEstimates.WeightStatus} | numer 1 = 1, równy = {NegotiationEstimates.StatusScoreEqual}, numer 2 = {NegotiationEstimates.StatusScoreNumberTwo}
Ryzyko (minus) | waga {NegotiationEstimates.WeightRisk} | długość, opcje, klauzula wyjścia
```

```kroki
Oferta | pensja, lata, status, opcje, klauzule
Odpowiedź | po kilku dniach, najwcześniej po {NegotiationEstimates.ResponseDelayMinDays|dni}
Rundy | {NegotiationEstimates.MinRounds}–{NegotiationEstimates.MaxRounds}, zależnie od charakteru
Decyzja | najlepsza oferta; przy remisie zaufanie
```

```pytania
Czy kierowca kiedyś odrzucił lepszą pensję albo wybrał słabszy zespół? Czy to miało dla Ciebie sens?
Czy {NegotiationEstimates.MinRounds}–{NegotiationEstimates.MaxRounds} rundy negocjacji to za mało, w sam raz czy za dużo?
Czy pensja jest zbyt silnym albo zbyt słabym argumentem?
```

---

## 6. Auto: koncepcja

Auto to zestaw osiągów, a jego koncepcja to sześć osi i każda ma swoją cenę. Koncepcja ma nazwę zespołu i rok, w którym weszła do auta (na przykład „Maserati 56”), i może jeździć kilka sezonów.

```wybory
Ewolucja czy rewolucja | bezpieczniejszy start albo wyższy sufit z większym rozrzutem
Kierunek aerodynamiki | na proste, wyważona albo na zakręty, z liczbami dla prostych i zakrętów
Kierowca do auta | rozjazd w balansie, trakcji i stylu hamowania kosztuje {CarEstimates.PaceSecondsPerMismatch|s} na okrążeniu za jednostkę
Nowa koncepcja | startuje ze zrozumieniem {CarEstimates.NewConceptUnderstanding} na 100 i rośnie z testami i kilometrami
```

```porownanie Ewolucja | Rewolucja
Sufit nowej koncepcji względem obecnej | +{DevelopmentEstimates.EvolutionCeilingShift} | +{DevelopmentEstimates.RevolutionCeilingShift}
Rozrzut sufitu | ±{DevelopmentEstimates.EvolutionCeilingSd} | ±{DevelopmentEstimates.RevolutionCeilingSd}
Auto na start | {DevelopmentEstimates.EvolutionStartFraction|%} sufitu | {DevelopmentEstimates.RevolutionStartFraction|%} sufitu
```

```pola
Auto na starcie kariery | startuje ze swoją siłą, a rozwój może ją podnieść o {CarEstimates.InitialHeadroom} punktów
Osiągi | moc · docisk · przyczepność mechaniczna · hamowanie · niezawodność | docisk ograniczony epoką
Osie koncepcji | aero · filozofia · okno pracy · chłodzenie · opony · silnik
Zrozumienie auta | brak zrozumienia kosztuje do {CarEstimates.UnderstandingMaxLoss} punktów w każdym obszarze poza mocą
```

```wykres koncepcja
Nikt nie zna sufitu dokładnie, także dyrektor techniczny.
```

```pytania
Czy rewolucja jest warta ryzyka?
Czy rozumiesz, czemu to samo auto pasuje jednemu kierowcy bardziej niż drugiemu?
Czy niepewność co do sufitu koncepcji jest ciekawa, czy frustrująca?
```

---

## 7. Rozwój auta

Części do auta, które jedzie, wybierają i dowożą inżynierowie, a Ty decydujesz o trzech rzeczach: jak podzielić ludzi, jaki charakter ma mieć następna koncepcja i kiedy ją wprowadzić. Wynik zależy od ludzi: od umiejętności i od innowacyjności, więc najdroższy sztab nie zawsze zbuduje najlepsze auto.

```wybory
Podział ludzi | suwak między autem, które jedzie, a następną koncepcją (domyślnie {DevelopmentEstimates.DefaultNextYearPercent|%%} na koncepcję)
Charakter następnej koncepcji | ewolucja albo rewolucja i kierunek aerodynamiki, z liczbami przy każdej opcji
Kiedy wprowadzić gotową koncepcję | od razu albo poczekać; budowa trwa kilka tygodni
```

```kroki
Projekt | powstaje przez {DevelopmentEstimates.ConceptDesignDays1955|dni} w 1955 przy domyślnym podziale; większy udział ludzi skraca czas
Gotowa | decyzja w skrzynce z zakresem sufitu, zyskiem, poziomem na starcie, czasem budowy, kosztem i pierwszym wyścigiem; po {DevelopmentEstimates.ConceptDecisionDays|dni} bez odpowiedzi czekamy
Budowa | koszt {DevelopmentEstimates.ConceptProductionCostShare|%} kosztu rozwoju, płatny od razu, bez anulowania
Stare auto jedzie | wyścigi do końca budowy
Nowa koncepcja | pierwszego dnia po budowie, nigdy w środku weekendu; zrozumienie auta spada
```

```pola
Zasięg koncepcji | może jeździć kilka sezonów, a co sezon traci {DevelopmentEstimates.ConceptAgingPerSeason} pkt sufitu
Zysk części | część dystansu do sufitu koncepcji | blisko sufitu każda dziesiątka kosztuje więcej
Przełom | szansa {DevelopmentEstimates.BreakthroughBase|%} u każdego inżyniera, u najbardziej innowacyjnych wyraźnie większa | część daje wtedy dużo więcej niż zakładano
Ludzie | skracają czas | nie podnoszą jakości
Porażka | {DevelopmentEstimates.BaseRisk|%} szansy przed umiejętnościami | koncepcja {DevelopmentEstimates.ConceptRiskMultiple}× ryzykowniejsza, a rewolucja jeszcze bardziej
Zakresy | auto widzisz w zakresach z oceny sztabu technicznego | lepszy sztab, węższy zakres
Rywale | czołowa trójka tylko jako szersze zakresy
```

```wykres czas-produkcji
```

```pytania
Czy suwak i charakter następnej koncepcji są zrozumiałe bez tłumaczenia?
Czy trzymanie jednej koncepcji kilka sezonów jest czasem lepsze od zmiany?
Czy czekanie z wprowadzeniem koncepcji bywa dla Ciebie prawdziwym dylematem?
```

---

## 8. Infrastruktura

Każdy zespół ma fabrykę. Jej jakość liczy się względem stanu techniki danego roku, więc bez modernizacji powoli się starzeje. Tor testowy wynajmujesz na konkretny test. Dojazd na wyścig to osobny koszt logistyki: w Europie ciężarówki, do Argentyny statek.

```wybory
Co rozbudować | fabryka (jakość i tempo części); tunel, CFD i symulator pojawiają się z epoką
Wynająć tor | jeden test, jeden koszt; limit testów zależy od przepisów sezonu
Kiedy płacić | rozbudowa z budżetu, obiekt w tym czasie pracuje gorzej; utrzymanie {InfrastructureEstimates.UpkeepShareAtFull|%} typowego budżetu na obiekt przy pełnej jakości, 1 stycznia
Czy gonić czołówkę | im bliżej stanu sztuki, tym mniejszy zysk i wyższy koszt kolejnego kroku
```

```pola
Jakość | względem granicy roku | granica rośnie o {InfrastructureEstimates.FrontierGrowthMilliPerYear|tys.} tysięcznych co sezon
W budowie | {InfrastructureEstimates.BuildingWorkShare|%} sprawności | aż do dnia końca
Pierwszy krok | około {InfrastructureEstimates.BaseUpgradeCostShare|%} typowego budżetu i {InfrastructureEstimates.BaseUpgradeDays|dni} | potem drożej i dłużej
Wynajem toru | około {InfrastructureEstimates.TestRentalShare|%} typowego budżetu za test
Ciężarówki | {InfrastructureEstimates.LogisticsLorryDays|dni} w Europie, około {InfrastructureEstimates.LogisticsLorryShare|%} typowego budżetu
Statek | {InfrastructureEstimates.LogisticsShipDays|dni} do rundy za oceanem (Argentyna), około {InfrastructureEstimates.LogisticsShipShare|%} typowego budżetu
Efekt | przez rozwój auta | nie przez sam poziom fabryki
```

```pytania
Czy czujesz, że bez modernizacji fabryka zostaje w tyle?
Czy wynajem toru jest jasnym kosztem za wiedzę, a nie kolejnym budynkiem?
Czy wyjazd do Argentyny jest wyczuwalnie droższy i dłuższy niż start w Europie?
Czy kolejny poziom fabryki jest wystarczająco drogi, żebyś się wahał?
```

---

## 9. Dostawcy

Rodzaj umowy z dostawcą decyduje, kiedy dostajesz nowości i ile płacisz.

```wybory
Fabryczna | nowości od razu, wspólny rozwój, ale zależność od dostawcy
Partnerska | 1 sezon opóźnienia, dostawca dodaje {SupplyEstimates.PartnerReliabilitySupport|pkt} niezawodności
Kliencka | 1 sezon opóźnienia, tanio, ten sam produkt dla wszystkich
Silnik z zeszłego roku | 2 sezony opóźnienia, najtańsza opcja awaryjna
Wyłączność | dopłata +{SupplyEstimates.ExclusiveMarkupMilli|m%} ceny
Dłuższa umowa | −{SupplyEstimates.YearsDiscountMilli|m%} za każdy kolejny sezon, najwyżej −{SupplyEstimates.YearsDiscountCapMilli|m%}
```

```pola
Postęp silnika | +{SupplyEstimates.ProgressPerSeason|pkt} na sezon
Losowa zmiana mocy silnika | co sezon każdy dostawca zyskuje albo traci do {SupplyEstimates.PowerDriftStep|pkt} mocy ponad ten postęp, a Ty na początku sezonu dostajesz wiadomość, czy Twój silnik wyszedł mocniejszy, słabszy czy podobny
Klienci jednego dostawcy | najwyżej {SupplyEstimates.MaxCustomersPerSupplier}
```

```pytania
Czy klient jest dla Ciebie wyraźnie wolniejszy niż zespół fabryczny?
Czy wybór dostawcy opon cokolwiek zmienia w wynikach?
Czy kusi Cię umowa na wiele sezonów z rabatem?
```

---

## 10. Sponsorzy

Masz trzy miejsca na sponsorów, a na każde kilku kandydatów.

```wybory
Podpisać od razu czy czekać | czekanie poprawia warunki o {SponsorEstimates.WaitingGainMilliPerDay|m%} dziennie, do limitu negocjatora; na razie nikt nie zabierze Ci sponsora
Który sponsor na które miejsce | miejsce dodatkowe płaci {SponsorEstimates.SecondarySlotMilli|m%} kwoty głównego
Cel sponsora | dopasowany do siły zespołu (oczekiwana pozycja, jak u zarządu); premia startuje od {SponsorEstimates.BonusMilli|m%} rocznej kwoty i rośnie z trudnością celu, niespełniony może zakończyć umowę
Przedłużenie | sponsor proponuje sam od {SponsorEstimates.RenewalMinTrust} zaufania
Odpowiedź na ofertę | oferta przedłużenia przychodzi do skrzynki z wyborem: przedłuż albo puść sponsora; bez odpowiedzi umowa po prostu się kończy. Gdy sponsor nie złoży oferty, dostajesz o tym wiadomość {SponsorEstimates.RenewalLeadDays|dni} przed końcem umowy
Własna pula | każdy zespół ma własnych sponsorów: co sezon {SponsorEstimates.LocalBackersPerSeason} nowych, innych propozycji na rodzaj miejsca, różnych nazwą, kwotą i celem; do tego dochodzą sponsorzy z nazwy, dostępni dla Ciebie co sezon; żaden sponsor nie jest wspólny, więc zespoły AI nigdy nie odbierają Ci propozycji
```

```kroki
Otwarcie | sponsor proponuje {SponsorEstimates.OpeningTermsMilli|m%} pełnej ceny
Czekanie | +{SponsorEstimates.WaitingGainMilliPerDay|m%} dziennie, do limitu negocjatora
Rywal | na razie żadnego: zespoły nie walczą o sponsorów, wspólny rynek sponsorów wejdzie później
Umowa | {SponsorEstimates.DealDays|dni}, raty co miesiąc, cel sponsora
```

```wykres sponsor-czekanie
Przerywana linia: ile średnio zdobędziesz, czekając tyle dni, gdyby rywal mógł podpisać pierwszy (po wprowadzeniu wspólnego rynku).
```

```pytania
Czy podpisałbyś sponsora od razu, czy czekał?
Skoro nikt nie zabiera sponsorów, czekanie zawsze się opłaca do limitu. Czy brakuje Ci tu ryzyka, czy wolisz spokój?
Czy cele sponsorów są zrozumiałe i uczciwe?
```

---

## 11. Pieniądze i zarząd

Każdy przychód i koszt trafia do księgi. Zarząd ocenia Cię po każdym wyścigu i może Cię zwolnić.

```wybory
Cel na sezon | w skrzynce, termin {BoardEstimates.SeasonTargetDecisionDays|dni}, domyślnie oczekiwany: bezpieczny (mała premia), oczekiwany albo ambitny (duża premia, porażka może kosztować posadę)
Na co wydać | rozwój auta i pensje to główne koszty; auto i rozwój płacisz co tydzień, pensje 1. dnia miesiąca
Zejść pod kreskę | saldo może być ujemne, na powrót masz cały sezon, potem niewypłacalność
Ryzykować przy słabych wynikach | zarząd zwalnia po {BoardEstimates.ReviewsToDismissBase}+ ocenach z rzędu pod progiem; starszy zespół jest cierpliwszy
Oferta pracy po zwolnieniu | wygasa po {BoardEstimates.OfferValidDays|dni}, a najpóźniej po {BoardEstimates.GuaranteeWindowDays|dni} zawsze jakaś jest
```

```porownanie Przychody | Koszty
Lata 50. | pieniądze startowe za każdy start, nagrody za miejsca | pensje 1. dnia miesiąca
Od 1968 | + sponsorzy | auto i rozwój co tydzień
Później | + TV, nagrody za konstruktorów | dostawy, wyjazdy na wyścigi
```

```pola
Reputacja menedżera | start {BoardEstimates.InitialReputationTenths|t} ze 100 | rośnie za wyniki ponad oczekiwania i tytuły, spada za zwolnienie
Zaufanie zarządu | start {BoardEstimates.InitialConfidenceTenths|t} | po wyścigu nadrabia {BoardEstimates.ReviewSmoothing|%} różnicy do celu
Cierpliwość | {BoardEstimates.PatienceBase} + wiek zespołu w latach | starszy zespół czeka dłużej
Ochrona nowego szefa | pierwszy pełny sezon | + {BoardEstimates.ProtectionDaysPerReputationPoint|dni} za punkt reputacji
Popularność | wyrównany sezon ją podnosi, dominacja obniża | walka kierowców o tytuł podnosi ją mniej niż walka kilku zespołów
Finanse rywali | niewidoczne | widzisz tylko swoje i prognozę do końca sezonu
```

```wykres zarzad-prog
```

```pytania
Czy zarząd zwalnia za szybko, za wolno, czy w sam raz?
Czy zejście pod kreskę to kusząca opcja, czy kara?
Czy wiesz, od czego zależą przychody Twojego zespołu?
```

---

## 12. Wyścig

Przed wyścigiem przygotowujesz zespół, w wyścigu pracują Twoi ludzie, a wynik powstaje okrążenie po okrążeniu.

```wybory
Strateg | steruje postojami i tempem; słabszy gorzej ocenia zużycie opon i plany. Jego umiejętność to w {RaceStaffEstimates.StrategyWeight|%} strategia, reszta to „Reakcja”; atrybut „Pogoda” decyduje o trafności prognozy. Rywale mają swoich strategów na tych samych zasadach
Ekipa w boksie | jej jakość to atrybut „Pit stopy” szefa mechaników; od niej zależy ryzyko błędu: +{PitConstants.ErrorMinSeconds|s}–{PitConstants.ErrorMaxSeconds|s} straty
Kierowca i auto pod tor | tor nagradza różne parametry auta
Jak oglądać wyścig | w dniu wyścigu gra przechodzi w tryb wyścigu: mapa toru z kropkami, które zwalniają w zakrętach, klasyfikacja, przebieg, a w radiu strateg i Twoi kierowcy; tempo ×1, ×5, ×10, ×20 i pauza; wyścig ogląda się w całości. Auto-pauza sama zatrzymuje wyścig przy fladze, deszczu i ważnej wieści o Twoim aucie, a komunikat pokazuje, co się stało. Kropka przy różnicy w klasyfikacji to walka o pozycję. Klasyfikację i panel boksu poszerzysz albo zwęzisz, przeciągając ich wewnętrzną krawędź; dwuklik przywraca szerokość domyślną
Szybki wyścig | z menu głównego, bez kariery: wybierasz sezon, zespół i rundę z kalendarza tego sezonu, a wyścig od razu rusza w trybie wyścigu. Świat jest taki, w jakim zaczęłaby się kariera w tym roku, wyścig liczy się według tych samych zasad, a po mecie wracasz do menu. Z szybkiego wyścigu możesz też wyjść w każdej chwili przyciskiem „Wyjdź” obok tempa; gra pyta wtedy, czy na pewno. Kariera w pamięci zostaje nietknięta i nic się nie zapisuje
Polecenia z boksu | w szybkim wyścigu możesz przejąć auto od stratega. Tempo kierowcy ma pięć stopni, od pełnego oszczędzania do tempa kwalifikacyjnego, a „Strateg” oddaje mu tempo z powrotem. Silnik ma trzy tryby: oszczędny, normalny i pełną moc. Polecenie zespołowe „Przepuść kolegę” każe kierowcy oddać miejsce koledze z zespołu, gdy ten jedzie tuż za nim. Tempo, silnik i polecenie zespołowe działają od następnego okrążenia. Zjazd: wybierasz opony i klikasz „Potwierdź zjazd”; auto zjeżdża na końcu okrążenia, a jeśli już minęło wjazd do boksu (po {LiveRaceOrders.PitCallShare|%} okrążenia jest na to za późno), okrążenie później. Zjazd można odwołać, dopóki auto nie minie wjazdu do boksu. Co już widziałeś, nie zmienia się: polecenie działa tylko na dalszą część wyścigu
Polecenia w karierze | na razie zablokowane: wynik wyścigu kariery jest zapisany w dniu wyścigu, zanim go obejrzysz, więc boks pokazuje dane aut, ale nie przyjmuje poleceń
```

```kroki
Baza toru | długość ÷ prędkość epoki × charakter toru
Auto | dopasowanie parametrów do fragmentów toru
Kierowca | tempo, regularność, opanowanie w deszczu
Paliwo i opony | ciężar baku, zużycie
Ruch | brudne powietrze, walka o pozycję
Rozrzut czasów | drobne różnice z okrążenia na okrążenie (błędy, ruch, wiatr); regularny kierowca jeździ równiej
```

```pola
Wyprzedzanie | potrzebna przewaga tempa powyżej {WeekendConstants.OvertakeMarginSeconds|s} | rośnie z obroną broniącego, maleje z wyprzedzaniem atakującego
Brudne powietrze | do {PaceConstants.DirtyAirMaxLossSeconds|s} straty na okrążeniu
Pierwsze okrążenie | {IncidentConstants.FirstLapFactor}× groźniejsze niż zwykle | drugie {IncidentConstants.SecondLapFactor}×
Pogoda | zmienia się minuta po minucie | prognoza w boksie ma błąd: najlepsza osoba {WeatherConstants.GoodForecasterScale}×, najsłabsza {WeatherConstants.PoorForecasterScale}×
Ostrzeżenie o awarii | {ReliabilityConstants.DefaultWarningLeadLaps} okrążenia wcześniej | auto zwalnia; około {ReliabilityConstants.SuddenFailureShare|%} awarii przychodzi nagle, bez ostrzeżenia
Pełne oszczędzanie | {PitConstants.ConservePaceLossSeconds|s} wolniej na okrążeniu | opony zużywają się {PitConstants.ConserveWearFactor}× tak szybko, paliwa schodzi {PitConstants.ConserveBurnFactor}× tyle; kierowca jedzie spokojniej, więc rzadziej ma wypadek
Oszczędzaj | {TyreFuelConstants.FuelSavingPaceLossSeconds|s} wolniej na okrążeniu | opony zużywają się {PitConstants.SaveWearFactor}× tak szybko, paliwa schodzi o {TyreFuelConstants.FuelSavingBurnReduction|%} mniej
Atak | {PitConstants.PushPaceGainSeconds|s} szybciej na okrążeniu | opony zużywają się {PitConstants.PushWearFactor}× szybciej, paliwa schodzi {PitConstants.PushBurnFactor}× więcej
Tempo kwalifikacyjne | {PitConstants.QualifyingPaceGainSeconds|s} szybciej na okrążeniu | opony zużywają się {PitConstants.QualifyingWearFactor}× szybciej, paliwa schodzi {PitConstants.QualifyingBurnFactor}× więcej, a kierowca ryzykuje: częściej ma wypadek
Silnik na pełnej mocy | {PitConstants.FullEnginePower|%} mocy | paliwa schodzi {PitConstants.FullEngineBurn}× więcej, a silnik i chłodzenie psują się {PitConstants.FullEngineHazard}× częściej. Moc daje najwięcej na torach, które ją nagradzają
Silnik oszczędny | {PitConstants.LeanEnginePower|%} mocy | paliwa schodzi {PitConstants.LeanEngineBurn}× tyle, a silnik i chłodzenie psują się {PitConstants.LeanEngineHazard}× tak często
Przepuść kolegę | gdy kolega jedzie do {PitConstants.LetByGapSeconds|s} za Twoim kierowcą | kierowca oddaje miejsce i traci jeszcze {PitConstants.LetByCostSeconds|s} na odpuszczeniu gazu
Kierowca w radiu | opony słabną po {TyreFeelBands.WornFromCliffShare|%} drogi do klifu | mówi też, gdy są skończone, a gdy nie wolno tankować, że paliwa przy tym tempie nie starczy do mety
```

```wykres dopasowanie-toru
Dlatego to samo auto jest na jednym torze z przodu, a na innym w środku stawki.
```

```wykres auto-kontra-kierowca
Ile na okrążeniu daje dodatkowe 10 punktów oceny kierowcy, a ile auta.
```

```wykres opony
Miękka szybsza na starcie, klif przychodzi wcześniej. W latach 50. jedna twarda opona na cały wyścig.
```

```wykres deszcz
Skalibrowane na wyścigach 1950–2025.
```

```wykres słupki
tytuł: Odsetek startujących, którzy odpadli przez awarię (cel gry)
format: %
max: 50
1950–59 | 43
1960–69 | 37
1970–79 | 33
1980–93 | 36
1994–2009 | 22
od 2010 | 8
opis: Gra dostraja ryzyko awarii tak, żeby trafić w te odsetki.
```

```wykres mechanicy
```

Do każdej rundy wracasz ze strony rundy. Wyniki są w zakładkach: kwalifikacje pokazują pole startowe i czas pole position, wyścig pełną klasyfikację z czasami, a pod spodem są poprzednie wyścigi na tym torze: podium z kolorami zespołów. Z zakładki Klasyfikacje otworzysz przegląd sezonu: w wierszach kierowcy (osobno konstruktorzy), w kolumnach rundy z flagą kraju, w komórce miejsce na mecie albo Ret, kolorem oznaczone podium, punkty i wycofania.

```pytania
Czy czujesz, że decyzje przed startem miały wpływ na wynik?
Czy wyprzedzania jest za dużo, za mało, czy w sam raz?
Czy awarii jest tyle, ile się spodziewasz w danej epoce?
Czy wyścig z 1955 i z 1988 wygląda inaczej? Napisz, w czym.
Czy szybki wyścig to dobry sposób na sprawdzenie jednego toru albo epoki? Czego Ci w nim brakuje?
Czy polecenia z boksu (tempo i zjazd) dają Ci coś do roboty w trakcie wyścigu? Kiedy chciałeś coś zrobić, a nie mogłeś?
```

---

## 13. Rywale i niepewność

Zespoły AI grają według tych samych zasad co Ty. Nie widzą ukrytych wartości i nie znają przyszłości.

```wybory
Kto zdąży pierwszy | rywal może podpisać sponsora albo kierowcę przed Tobą
Kogo podkupić | kierowcy i personel oceniają oferty wszystkich zespołów, nie tylko Twojej
Jak ocenić rywala | widzisz wyniki, czasy i klasyfikacje; jego finansów, umów i auta nie
```

```porownanie Widzisz | Nie widzisz
Kierowcy | swoich dokładnie po czasie, cudzych w widełkach | ukrytego potencjału
Auto | przedziały zysku i sufitu | prawdziwego sufitu
Rywale | wyniki, czasy, klasyfikacje | finansów, umów, wektora auta
Pogoda | prognozę z błędem | przyszłej pogody
```

```pytania
Czy rywale zachowują się bezmyślnie, wszechwiedząco, czy wiarygodnie? Opisz konkretną sytuację.
Czy gdzieś w grze widać dokładną wartość, której nie powinieneś znać?
Czy chciałbyś grać w trybie bez liczb, w którym ludzie mówią słowami, np. „młody jest szybki, ale zjada opony”?
```

---

## 14. Przepisy i głosowania

Przy starcie kariery wybierasz, czy przepisy idą swoim historycznym torem, czy co sezon głosują nad nimi zespoły. Gdy głosują, Ty też masz głos, a Twój zespół może zapłacić za własną propozycję. Każda zmiana, także w kalendarzu, wchodzi dopiero od pierwszego wyścigu sezonu po głosowaniu.

```wybory
Tryb przepisów | historyczny (bez głosowania, wszystko jak w prawdziwej historii), głosowany z jednym głosem na zespół albo głosowany z bankiem głosów
Jak zagłosować | za wariantem, za obecnym przepisem albo wstrzymać się; do terminu możesz zmienić zdanie
Czy zapłacić za propozycję | zgłaszasz zmianę wybranego przepisu albo toru w kalendarzu, ale płacisz opłatę i przez {RegulationEstimates.CooldownSeasons|sezonów} nie zgłosisz kolejnej
Co zrobić z głosem w trybie z bankiem | zagłosować od razu albo wstrzymać się i odłożyć głos do banku na ważniejszą sprawę
Jak się dogadać z rywalami | nie ma lobbingu; skłonności zespołów AI widzisz po ich głosach i powodach
```

```kroki
Od pierwszego dnia sezonu | okno zgłoszeń: zespoły płacą za propozycje; propozycje na ten sam przepis scalają się w jedno głosowanie z wariantami
Przerwy między wyścigami | głosowania są rozłożone równo przez cały sezon, każde w przerwie między dwoma weekendami wyścigowymi: od dnia po wyścigu do dnia przed następnym weekendem; w roku jest od {RegulationEstimates.FiaVotesMin} do {RegulationEstimates.FiaVotesMax} głosowań FIA i jedno nad propozycjami zespołów, mniej więcej w środku sezonu
Dzień przed głosowaniem zespołów | okno zgłoszeń się zamyka; następnego dnia propozycje stają się głosowaniem
Termin | głosowanie jest liczone przed następnym weekendem wyścigowym, nigdy w jego trakcie; kto nie zagłosował, wstrzymał się; wynik z powodem trafia do skrzynki
Pierwszy wyścig kolejnego sezonu | przyjęta zmiana zaczyna działać; sezon, w którym głosowano, jedzie po staremu
```

```pola
Bank głosów | wstrzymanie się odkłada jeden głos; bank nie ma limitu | na jedno głosowanie wydasz tyle, ile masz wolnych głosów, bez limitu na pozycję; bank nie spada poniżej zera
Remis | rozstrzyga prezydent FIA | tylko wtedy; przy wyraźnej przewadze jego głos nic nie zmienia
Opłata za propozycję | {RegulationEstimates.FeeRevenueShare|%} przychodu z ostatniego zakończonego sezonu | nie zależy od gotówki, więc czekanie na gorszy moment nic nie daje; nie wraca, także gdy propozycja przepadnie
Dolna granica opłaty | {RegulationEstimates.FeeFloorShareOfTypicalBudget|%} typowego budżetu epoki | opłata nigdy nie jest zerem
Zejście pod kreskę | opłatę możesz zapłacić nawet wtedy, gdy saldo spadnie poniżej zera | zadłużenie ma swoje skutki w finansach
Karencja | po propozycji w sezonie N nie zgłosisz nowej w N+1 i N+2, a w N+3 znów możesz | dotyczy tylko zespołu; przepis, który właśnie się zmienił, może zmienić ponownie FIA albo inny zespół
Start kariery | zespoły AI mają różne karencje startowe, od 0 do {RegulationEstimates.StartingCooldownMaxSeasons|sezonów} | Ty zaczynasz bez karencji
Kalendarz | można skreślić wyścig, dodać wyścig albo zmienić układ toru | kalendarz zachowuje co najmniej {RegulationEstimates.MinimumRounds} rund
```

```porownanie Jeden głos na zespół | Bank głosów
Wstrzymanie się | głos przepada | głos trafia do Twojego banku
Ważna sprawa | masz jeden głos jak zawsze | możesz dołożyć zbankowane głosy
Ryzyko | niczego nie oszczędzasz | odłożone głosy nie wygasają, ale nie pomogą w głosowaniu, które właśnie mija; zespoły AI też wydają swoje banki, więc nie czekaj w nieskończoność
```

```wybory
Co możesz zmienić głosowaniem | punktację (tabela, punkt za najszybsze okrążenie, podwójne punkty w finale, ile wyników się liczy, punkty konstruktorów), format kwalifikacji, samochód bezpieczeństwa, tankowanie, dystans wyścigu i kalendarz
Czego nigdy nie da się zmienić | zasad i mechanik z listy zakazanych swojej serii: to stały rdzeń mistrzostw, FIA i zespoły AI ich nie proponują, a Twoja próba kończy się odmową z powodem; na razie na liście każdej serii są ładowanie odzysku energii zależne od pozycji oraz premie pieniężne dla ostatniego i dla awansującego zespołu (zostają w katalogu jako możliwe kiedyś, ale nie działają)
Czego jeszcze nie da się zmienić | czerwone flagi, odwróconej kolejności startowej, sprintów, długości sesji, opon, pit-stopów, DRS i ERS, części typowych ani podziału nagród; silnik wyścigu jeszcze tego nie symuluje
```

```pola
Skłonność zespołu | tradycjonalista, postępowiec, egalitarysta albo showman | stała; do własnego interesu dokłada głos AI
Jak głosuje AI | według własnego interesu i skłonności | zna tylko publiczną tabelę, swoje finanse i własny kraj, nigdy ukrytych wartości ani przyszłości
Kiedy AI składa propozycję | gdy spodziewa się zysku większego niż opłata, zadłużenie i trzy sezony karencji | zadłużony zespół nie wyda opłaty na przepis, a zakazanej zasady nie proponuje nigdy
Powód wyniku | zawsze zapisany | widzisz, kto jak głosował i dlaczego
```

```pytania
Czy wiesz, co zagłosowano, kiedy zmiana zacznie działać i dlaczego tak wyszło?
Czy bank głosów daje sensowny wybór, czy jest dodatkową księgowością?
Czy opłata i karencja sprawiają, że propozycja jest decyzją, a nie klikaniem?
Czy zmiany przepisów i kalendarza zmieniają wyścigi tak, jak się spodziewałeś?
```
