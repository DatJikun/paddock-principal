# Jak działa gra

Przewodnik dla osób, które testują grę. Pokazuje, co w grze wybierasz i jak te wybory działają. Na końcu każdego rozdziału są pytania, na które chcemy znać Twoją odpowiedź.

```pola
Po co to czytasz | żeby ocenić, jak te wybory działają w grze | każdy rozdział kończy się pytaniami
Liczby | to wstępne ustawienia | mogą się zmienić po Waszych uwagach
Jak odpowiedzieć | napisz, co czujesz | np. „sponsorzy podpisują z rywalami za szybko”, bez żadnych liczb
```

---

## 1. Start kariery

Zaczynasz od wyboru roku i zespołu, a potem ustawiasz, jak mocno świat trzyma się prawdziwej historii.

```wybory
Rok i zespół | pierwszy sezon do testów to 1955, zespół przejmujesz od razu
Preset | najbardziej historyczny, zbalansowany albo chaos
Ludzie | prawdziwa kariera rok po roku, prawdziwy sufit talentu, prawdziwe nazwiska z losowymi umiejętnościami albo wszyscy generowani
Przepisy | historyczne albo głosowane co sezon
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

Nowi ludzie wchodzą do gry przez pulę talentów, czyli świat poza F1. Skauci pomagają odgadnąć, kto z niej wyrośnie.

```wybory
Obserwuj całą pulę | wolno, wszyscy naraz
Obserwuj jedną osobę | szybko, jej widełki się zawężają
Opłać sezon juniorski | tani i wolny albo drogi i szybki
Podpisz | kierowca wyścigowy, testowy albo junior z opcją
Akademia | ograniczone miejsca; poziom akademii daje lepszych juniorów, ale kosztuje co sezon; około 1 na 10 juniorów nie dochodzi do potencjału
```

```pola
Prawdziwi kierowcy | trafiają do puli {PeopleScheduleRules.DefaultPoolLeadYears|lat} przed debiutem | najwcześniej w wieku {PeopleScheduleRules.PoolMinimumAgeYears|lat}
Fikcyjni juniorzy | pula uzupełniana do {PoolEstimates.TargetSize|osób} co sezon | AI nie wie, kto jest prawdziwy
Odpada | po {PoolEstimates.MaxSeasonsInPool|sezonów} bez kontraktu albo po {PoolEstimates.MaxAge|lat}
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
Czy junior z programem jest gotowy w sensownym czasie?
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
Gdy rywal kusi Twojego kierowcę | w ostatnim roku umowy; zależnie od morale i lojalności kierowca odchodzi, prosi o lepsze warunki albo zostaje
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

Auto to zestaw osiągów, a jego koncepcja to sześć osi i każda ma swoją cenę.

```wybory
Ewolucja czy rewolucja | bezpieczniejszy start albo wyższy sufit z większym rozrzutem
Kierowca do auta | rozjazd w balansie, trakcji i stylu hamowania kosztuje {CarEstimates.PaceSecondsPerMismatch|s} na okrążeniu za jednostkę
Nowa koncepcja | startuje ze zrozumieniem {CarEstimates.NewConceptUnderstanding} na 100 i rośnie z kilometrami i pracą
```

```porownanie Ewolucja | Rewolucja
Średni sufit | {CarEstimates.EvolutionCeilingMean} | {CarEstimates.RevolutionCeilingMean}
Rozrzut sufitu | ±{CarEstimates.EvolutionCeilingSd} | ±{CarEstimates.RevolutionCeilingSd}
Auto na start | {CarEstimates.EvolutionStartFraction|%} sufitu | {CarEstimates.RevolutionStartFraction|%} sufitu
```

```pola
Osiągi | moc · docisk · przyczepność mechaniczna · hamowanie · niezawodność | docisk ograniczony epoką
Osie koncepcji | aero · filozofia · okno pracy · chłodzenie · opony · silnik
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

Zasoby dzielisz Ty, a konkretne projekty wybierają inżynierowie.

```wybory
Podział zasobów | bieżące auto (domyślnie {DevelopmentEstimates.DefaultCurrentPercent|%%}), konto rozwoju ({DevelopmentEstimates.DefaultAccountPercent|%%}) albo przyszły rok ({DevelopmentEstimates.DefaultNextYearPercent|%%})
Priorytety obszarów | aerodynamika, podwozie, niezawodność, opony, każdy w skali 0–10
Zatwierdzić koncepcję czy czekać | punkty teraz albo lepsze auto później
Inżynier prosi o czas | trzymać plan albo ciąć projekt (zostaje to, co zrobiono)
```

```kroki
Gotowa | decyzja w skrzynce; po {DevelopmentEstimates.ConceptDecisionDays|dni} bez odpowiedzi rozwijamy dalej
Produkcja | koszt {DevelopmentEstimates.ConceptProductionCostShare|%} kosztu rozwoju, płatny od razu, bez anulowania
Stare auto jedzie | wyścigi do końca produkcji
Nowe auto | pierwszego dnia po produkcji, nigdy w środku weekendu
```

```pola
Konto rozwoju | wiedza na później | traci wartość, gdy rywale idą do przodu
Zysk | część dystansu do sufitu koncepcji | blisko sufitu każda dziesiątka kosztuje więcej
Ludzie | skracają czas | nie podnoszą jakości
Porażka | {DevelopmentEstimates.BaseRisk|%} szansy przed umiejętnościami | koncepcja {DevelopmentEstimates.ConceptRiskMultiple}× ryzykowniejsza
Projekty naraz | 1 na {DevelopmentEstimates.HeadcountPerSlot|osób} inżynierów | najwyżej {DevelopmentEstimates.MaxSlots}
```

```wykres konto-rozwoju
Zmiana przepisów zabiera dodatkową część konta.
```

```wykres czas-produkcji
```

```pytania
Czy podział na bieżące auto, konto i przyszły rok jest zrozumiały bez tłumaczenia?
Czy wolisz wybierać konkretne projekty zamiast ustawiać priorytety?
Czy czekanie z zatwierdzeniem koncepcji bywa dla Ciebie prawdziwym dylematem?
```

---

## 8. Dostawcy

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
Klienci jednego dostawcy | najwyżej {SupplyEstimates.MaxCustomersPerSupplier}
```

```pytania
Czy klient jest dla Ciebie wyraźnie wolniejszy niż zespół fabryczny?
Czy wybór dostawcy opon cokolwiek zmienia w wynikach?
Czy kusi Cię umowa na wiele sezonów z rabatem?
```

---

## 9. Sponsorzy

Masz trzy miejsca na sponsorów, a na każde kilku kandydatów.

```wybory
Podpisać od razu czy czekać | czekanie poprawia warunki o {SponsorEstimates.WaitingGainMilliPerDay|m%} dziennie, ale rywal może podpisać pierwszy
Który sponsor na które miejsce | miejsce dodatkowe płaci {SponsorEstimates.SecondarySlotMilli|m%} kwoty głównego
Cel sponsora | dopasowany do siły zespołu (oczekiwana pozycja, jak u zarządu); premia startuje od {SponsorEstimates.BonusMilli|m%} rocznej kwoty i rośnie z trudnością celu, niespełniony może zakończyć umowę
Przedłużenie | sponsor proponuje sam od {SponsorEstimates.RenewalMinTrust} zaufania
```

```kroki
Otwarcie | sponsor proponuje {SponsorEstimates.OpeningTermsMilli|m%} pełnej ceny
Czekanie | +{SponsorEstimates.WaitingGainMilliPerDay|m%} dziennie, do limitu negocjatora
Ryzyko | {SponsorEstimates.RivalPresenceChance|%} szans, że rozmawia też rywal; podpisuje z szansą {SponsorEstimates.RivalSignChance|%} dziennie
Umowa | {SponsorEstimates.DealDays|dni}, raty co miesiąc, cel sponsora
```

```wykres sponsor-czekanie
Przerywana linia: ile średnio zdobędziesz, czekając tyle dni, gdy nie wiesz, czy rywal jest przy stole.
```

```pytania
Czy podpisałbyś sponsora od razu, czy czekał?
Według wykresu czekanie średnio się nie opłaca, chyba że dobry negocjator widzi, że rywala nie ma. Czy to dla Ciebie dylemat, czy oczywista decyzja?
Czy cele sponsorów są zrozumiałe i uczciwe?
```

---

## 10. Pieniądze i zarząd

Każdy przychód i koszt trafia do księgi. Zarząd ocenia Cię po każdym wyścigu i może Cię zwolnić.

```wybory
Cel na sezon | wybierasz: bezpieczny (mała premia), oczekiwany albo ambitny (duża premia, porażka może kosztować posadę)
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

## 11. Wyścig

Przed wyścigiem przygotowujesz zespół, w wyścigu pracują Twoi ludzie, a wynik powstaje okrążenie po okrążeniu.

```wybory
Strateg | steruje postojami i tempem; słabszy gorzej ocenia zużycie opon i plany
Ekipa w boksie | od jej jakości zależy ryzyko błędu: +{PitConstants.ErrorMinSeconds|s}–{PitConstants.ErrorMaxSeconds|s} straty
Kierowca i auto pod tor | tor nagradza różne parametry auta
Jak oglądać wyścig | na żywo, ×5, ×10, ×20 albo sam wynik
Ręczna kontrola | w planach jako opcja kariery: ręczne pit-stopy i polecenia tempa
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
Ostrzeżenie o awarii | {ReliabilityConstants.DefaultWarningLeadLaps} okrążenia wcześniej | auto zwalnia; około {ReliabilityConstants.SuddenFailureShare} awarii przychodzi nagle, bez ostrzeżenia
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

```pytania
Czy czujesz, że decyzje przed startem miały wpływ na wynik?
Czy wyprzedzania jest za dużo, za mało, czy w sam raz?
Czy awarii jest tyle, ile się spodziewasz w danej epoce?
Czy wyścig z 1955 i z 1988 wygląda inaczej? Napisz, w czym.
```

---

## 12. Rywale i niepewność

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
