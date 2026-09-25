# Paddock Principal — Save Format & Database Architecture

**Wersja:** 1.0  
**Status:** DRAFT / APPROVED FOR PRE-PRODUCTION  
**Cel:** Specyfikacja relacyjnego systemu zapisu stanu gry opartego o SQLite (`Microsoft.Data.Sqlite`). Zapewnia transakcyjność, natychmiastowe ładowanie, brak puchnięcia pamięci RAM w 50-letnich karierach oraz obsługę migracji schematów.

---

## 1. Dlaczego SQLite zamiast JSON dla Plików Zapisu?

W starym prototypie (GDD V4) planowano zapisywać cały stan gry w jednym potężnym pliku JSON. W zaawansowanym menedżerze sportowym to ślepa uliczka:
1. **Puchnięcie pliku**: Po 30 sezonach historia wyścigów, transferów, statystyk kierowców i rozwoju podzespołów waży dziesiątki megabajtów. Serializacja takiego JSON-a po każdym wyścigu powoduje zauważalne zacięcia (stutter).
2. **Zużycie pamięci**: Deserializacja gigantycznego drzewa obiektów zajmuje setki megabajtów RAM.
3. **Brak bezpieczeństwa danych**: Awaria prądu lub błąd w trakcie zapisu JSON-a bezpowrotnie niszczy cały plik.

**SQLite rozwiązuje wszystkie te problemy**:
- **ACID i Transakcyjność**: Zapis to ułamek sekundy (`WAL mode` — Write-Ahead Logging). W razie awarii stan gry jest zawsze spójny.
- **Odczyt na żądanie (Lazy Queries)**: Wyświetlenie tabeli mistrzostw z 1998 roku to jedno proste zapytanie SQL `SELECT`, bez konieczności ładowania do pamięci wszystkich danych o wyścigach z tamtego roku.
- **Strukturalna historia**: Statystyki kariery, rekordy torów i drzewa genealogiczne kierowców są naturalnie relacyjne.

Plik zapisu kariery ma rozszerzenie `.paddock` i jest w 100% standardową bazą SQLite.

---

## 2. Schemat Relacyjny Bazy Danych (DDL)

### 2.1. Metadane Zapisu: `save_meta`
```sql
CREATE TABLE save_meta (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    schema_version INTEGER NOT NULL,
    save_created_at TEXT NOT NULL,
    last_saved_at TEXT NOT NULL,
    career_name TEXT NOT NULL,
    manager_name TEXT NOT NULL,
    player_team_id TEXT NOT NULL,
    current_season_year INTEGER NOT NULL,
    current_calendar_day INTEGER NOT NULL,
    content_pack_id TEXT NOT NULL,
    content_pack_hash TEXT NOT NULL,
    rng_master_seed INTEGER NOT NULL
);
```

---

### 2.2. Zespoły i Infrastruktura: `teams`
```sql
CREATE TABLE teams (
    team_id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    short_code TEXT NOT NULL,
    livery_color_hex TEXT NOT NULL,
    budget_cents INTEGER NOT NULL,            -- Waluta w centach (unikanie błędów float)
    prestige INTEGER NOT NULL,                 -- 1-100
    factory_aero_level INTEGER NOT NULL,      -- 1-10
    factory_engine_level INTEGER NOT NULL,
    factory_chassis_level INTEGER NOT NULL,
    factory_simulator_level INTEGER NOT NULL,
    pit_crew_training_level INTEGER NOT NULL,
    engine_supplier_team_id TEXT,             -- NULL jeśli zespół jest fabryczny (works)
    FOREIGN KEY (engine_supplier_team_id) REFERENCES teams(team_id)
);
```

---

### 2.3. Personel i Kontrakty: `staff` i `contracts`
```sql
CREATE TABLE staff (
    staff_id TEXT PRIMARY KEY,
    team_id TEXT,                             -- NULL jeśli wolny agent
    role TEXT NOT NULL,                       -- TechnicalDirector, HeadOfStrategy, RaceEngineer, PitChief
    first_name TEXT NOT NULL,
    last_name TEXT NOT NULL,
    nationality TEXT NOT NULL,
    birth_year INTEGER NOT NULL,
    skill_aero INTEGER NOT NULL,              -- 1-100
    skill_engine INTEGER NOT NULL,
    skill_chassis INTEGER NOT NULL,
    skill_strategy INTEGER NOT NULL,
    skill_adaptability INTEGER NOT NULL,
    FOREIGN KEY (team_id) REFERENCES teams(team_id)
);

CREATE TABLE contracts (
    contract_id TEXT PRIMARY KEY,
    entity_type TEXT NOT NULL,                -- 'Driver' lub 'Staff'
    entity_id TEXT NOT NULL,
    team_id TEXT NOT NULL,
    salary_cents_per_year INTEGER NOT NULL,
    bonus_per_point_cents INTEGER NOT NULL,
    start_season_year INTEGER NOT NULL,
    expiry_season_year INTEGER NOT NULL,
    priority_role TEXT NOT NULL,              -- FirstDriver, EqualStatus, SecondDriver, Reserve
    buyout_clause_cents INTEGER NOT NULL,
    FOREIGN KEY (team_id) REFERENCES teams(team_id)
);
```

---

### 2.4. Kierowcy i Osiągi: `drivers`
```sql
CREATE TABLE drivers (
    driver_id TEXT PRIMARY KEY,
    team_id TEXT,                             -- NULL jeśli wolny agent
    first_name TEXT NOT NULL,
    last_name TEXT NOT NULL,
    nationality TEXT NOT NULL,
    birth_date TEXT NOT NULL,
    pace INTEGER NOT NULL,
    consistency INTEGER NOT NULL,
    focus INTEGER NOT NULL,
    wet_weather INTEGER NOT NULL,
    tire_management INTEGER NOT NULL,
    starts INTEGER NOT NULL,
    aggression INTEGER NOT NULL,
    attacking INTEGER NOT NULL,
    defending INTEGER NOT NULL,
    braking INTEGER NOT NULL,
    career_phase TEXT NOT NULL,               -- Rising, Peak, Plateau, Decline
    peak_age INTEGER NOT NULL,
    decline_age INTEGER NOT NULL,
    skill_ceiling INTEGER NOT NULL,           -- Ukryty potencjał
    is_retired INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (team_id) REFERENCES teams(team_id)
);
```

---

### 2.5. Wyniki Sesji i Archiwum Kariery: `race_results`
```sql
CREATE TABLE race_results (
    result_id INTEGER PRIMARY KEY AUTOINCREMENT,
    season_year INTEGER NOT NULL,
    round_index INTEGER NOT NULL,
    track_id TEXT NOT NULL,
    session_type TEXT NOT NULL,               -- 'Qualifying' lub 'Race'
    finish_position INTEGER NOT NULL,
    grid_position INTEGER NOT NULL,
    driver_id TEXT NOT NULL,
    team_id TEXT NOT NULL,
    total_time_ms INTEGER,                    -- Czas całkowity w milisekundach
    gap_to_leader_ms INTEGER,
    laps_completed INTEGER NOT NULL,
    points_awarded INTEGER NOT NULL,
    status TEXT NOT NULL,                     -- Finished, DNF_Mechanical, DNF_Crash, DSQ
    fastest_lap_time_ms INTEGER,
    stops_count INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (driver_id) REFERENCES drivers(driver_id),
    FOREIGN KEY (team_id) REFERENCES teams(team_id)
);
```

---

## 3. Kompaktowanie Historii (Retention & Compaction)

Po zakończeniu sezonu silnik uruchamia procedurę kompaktowania:
1. Szczegółowe czasy sektorów z poszczególnych okrążeń wyścigu zostają usunięte z bazy roboczej.
2. Zachowywane są trwałe rekordy końcowe (`race_results`), tabele klasyfikacji końcowej mistrzostw oraz rekordy torów (najszybsze okrążenia w historii).
3. Dzięki temu baza danych kariery po 50 sezonach rośnie maksymalnie do 8–15 MB, zachowując błyskawiczny czas dostępu.

---

## 4. Migracje Schematu (Schema Migrations)

Projekt wykorzystuje wbudowany system migracji wersjonowanych (`Paddock.Infrastructure.Migrations`):
- Każda zmiana struktury bazy to deterministyczny skrypt SQL w kodzie C# (np. `V001__InitialSchema.cs`, `V002__AddDirtyAirTracking.cs`).
- Przy otwarciu pliku zapisu sprawdzana jest wartość `schema_version` w tabeli `save_meta`.
- Jeśli plik pochodzi ze starszej wersji gry, silnik wewnątrz jednej transakcji aplikuje brakujące migracje.
