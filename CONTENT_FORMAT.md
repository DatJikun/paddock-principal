# Paddock Principal — Content Format & Modding Specification

**Wersja:** 1.0  
**Status:** DRAFT / APPROVED FOR PRE-PRODUCTION  
**Cel:** Specyfikacja formatu plików zawartości (Content Packs) opartych o JSON. Architektura umożliwia łatwe dodawanie sezonów historycznych, lig fikcyjnych, modów społeczności oraz skryptowanych zdarzeń kalendarzowych.

---

## 1. Struktura Paczki Zawartości (Content Pack Layout)

Wszystkie dane świata gry są ładowane z katalogu `content/`. Każda seria lub sezon stanowi oddzielny, samowystarczalny pakiet danych:

```text
content/
├── f1_2024/
│   ├── pack.json              # Metadane paczki (id, nazwa, wersja, autor)
│   ├── regulations.json       # Przepisy sportowe, techniczne i punkty
│   ├── calendar.json          # Lista wyścigów i daty w sezonie
│   ├── teams.json             # Zespoły, fabryki, budżety, barwy
│   ├── drivers.json           # Kierowcy, atrybuty, cechy, kontrakty
│   ├── staff.json             # Inżynierowie, szefowie strategii, dyrektorzy
│   ├── tracks.json            # Profile torów, długości, wagi osiągów
│   └── events.json            # Wydarzenia historyczne i narracyjne
└── f1_1994/
    ├── pack.json
    ├── regulations.json       # Tankowanie: TAK, DRS: NIE, brak Budget Cap
    ├── calendar.json
    ├── teams.json             # Williams, Benetton, Ferrari, Simtek...
    ├── drivers.json           # Senna, Schumacher, Hill, Ratzenberger...
    ├── tracks.json            # Dawna Imola, stary Hockenheim, Adelaide...
    └── events.json            # Skryptowane zdarzenia historyczne
```

---

## 2. Kluczowe Pliki i Schematy JSON

### 2.1. Metadane: `pack.json`
```json
{
  "$schema": "../schemas/pack.schema.json",
  "id": "f1_2024_official",
  "name": "Formula 1 — 2024 Championship Season",
  "version": "1.0.0",
  "author": "Paddock Principal Core Team",
  "seasonYear": 2024,
  "description": "Oficjalna stawka F1 2024: 10 zespołów, 24 tory, era efektu przyziemnego i limitu budżetowego.",
  "dependencies": []
}
```

---

### 2.2. Regulacje Serii: `regulations.json`
Określa reguły gry dla danego sezonu. Umożliwia łatwe modelowanie różnych epok:
```json
{
  "pointsSystem": {
    "race": [25, 18, 15, 12, 10, 8, 6, 4, 2, 1],
    "fastestLapPointEligibleTop": 10
  },
  "carsPerTeam": 2,
  "tireRegulations": {
    "mandatoryDifferentCompoundsInDryRace": true,
    "slickCompoundsAvailablePerWeekend": 3,
    "wetTiresAllowed": true
  },
  "refuelingAllowed": false,
  "drsEnabled": true,
  "budgetCapUsd": 135000000,
  "aeroTestingRestrictions": {
    "windTunnelHoursPerPeriod": 400,
    "slidingScaleByChampionshipPosition": true
  }
}
```

---

### 2.3. Kierowcy: `drivers.json`
```json
{
  "drivers": [
    {
      "id": "driver_max_verstappen",
      "firstName": "Max",
      "lastName": "Verstappen",
      "nationality": "NLD",
      "birthDate": "1997-09-30",
      "attributes": {
        "pace": 98,
        "consistency": 96,
        "focus": 94,
        "wetWeather": 99,
        "tireManagement": 93,
        "starts": 92,
        "aggression": 88,
        "attacking": 97,
        "defending": 96,
        "braking": 98
      },
      "career": {
        "phase": "Peak",
        "peakAge": 27,
        "declineAge": 36,
        "skillCeiling": 99,
        "declineRate": 0.05
      },
      "traits": ["Qualifier", "RainMaster", "IceCold"]
    }
  ]
}
```

---

### 2.4. Wydarzenia Historyczne: `events.json`
Jedna z najważniejszych funkcji wyróżniających Paddock Principal: możliwość przeżywania historycznych momentów lub tworzenia historii alternatywnych.

Tryby działania w grze (wybierane przy tworzeniu nowej kariery):
1. **Strict Historical**: Wszystkie wydarzenia odpalają się dokładnie tak, jak w historii (np. transfer Schumachera do Ferrari na sezon 1996, tragedia na Imoli 1994, wycofanie fabrycznych zespołów).
2. **Dynamic / Randomized**: Zdarzenia mają szansę zajść w oknie +/- 1 roku lub zależą od formy sportowej zespołów.
3. **Sandbox / Disabled**: Całkowity brak skryptów — świat rozwija się w 100% z symulacji AI i decyzji gracza.

Przykładowy rekord wydarzenia:
```json
{
  "events": [
    {
      "id": "event_imola_1994_incident",
      "title": "Czarny Weekend na Imoli",
      "seasonYear": 1994,
      "calendarRound": 3,
      "triggerCondition": "AtRaceWeekend",
      "actionType": "HighRiskIncidentModifier",
      "targetTrackId": "track_imola",
      "parameters": {
        "fatalityRiskMultiplier": 5.0,
        "historicalVictimDriverId": "driver_ayrton_senna",
        "allowAlternativeOutcome": true
      }
    },
    {
      "id": "event_schumacher_ferrari_1996",
      "title": "Transfer Stulecia",
      "seasonYear": 1995,
      "calendarRound": 16,
      "triggerCondition": "SeasonEndNegotiations",
      "actionType": "EnforceDriverTransfer",
      "parameters": {
        "driverId": "driver_michael_schumacher",
        "targetTeamId": "team_ferrari",
        "salaryUsd": 25000000,
        "contractLengthYears": 2
      }
    }
  ]
}
```

---

## 3. Walidacja i Bezpieczeństwo Modów

Przed załadowaniem paczki do silnika:
1. `ContentPackValidator` sprawdza poprawność plików względem schematów JSON Schema.
2. Weryfikowana jest spójność referencji (np. czy kierowca przypisany w zespole istnieje w bazie `drivers.json`, czy tor przypisany w kalendarzu ma profil w `tracks.json`).
3. Wyliczany jest unikalny hash zawartości (`ContentPackHash`), który zostaje zapisany w pliku zapisu SQLite. Zapewnia to, że wczytanie save'a wymaga identycznej wersji danych, co gwarantuje pełny determinizm symulacji.
