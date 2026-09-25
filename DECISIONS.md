# Paddock Principal — DECISIONS

**Cel:** stabilne ustalenia właściciela, do których odwołują się inne dokumenty. Zmiana decyzji to nowy wpis, a nie cicha edycja starego.
Numeracja jest własna (`PP-xxx`). Tam, gdzie decyzja pochodzi z Peloton Managera, podany jest jej odpowiednik.

## Przyjęte 2026-09-25

**PP-001: Flagowy tryb to historyczne F1 od 1950.** Pierwsza grywalna wersja to kariera od dowolnego roku 1950+ z prawdziwymi ludźmi. Endurance, GT i serie juniorskie powstają później na tym samym silniku.

**PP-002: Stack: C#/.NET 9 (rdzeń headless) + Godot 4.7 mono C# (UI), zapis w SQLite.** Infrastrukturę (determinizm, zapis, kalendarz, skrzynkę, kontrakty) przenosimy z Peloton Managera, zamiast pisać ją od zera. Stary kod w Pythonie nie istnieje i nie jest punktem odniesienia.

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

**PP-010: Pieniądz to indeks epoki bez automatycznej inflacji** (odpowiednik Peloton D-012). Wartości są przechowywane w jednostce znormalizowanej dla epoki, a UI może pokazać kwotę nominalną.

**PP-011: Oceny kierowców historycznych są wyliczane, a nie wpisywane ręcznie.** Pipeline danych wyprowadza je z wyników (porównania z partnerem z zespołu z odjęciem efektu samochodu w danym sezonie). Ręczne korekty to jawny, opisany plik nadpisań.

**PP-012: Test wierności historii to bramka jakości.** Symulacja okresu (np. 1950–1960) bez gracza musi dawać wiarygodne rezultaty w stosunku do prawdziwych: dominujący kierowcy są w czołówce, a nie w loterii. Nie oczekujemy identycznych wyników.

**PP-013: UI pokazuje umiejętności w skali 1–20.** Wewnętrzne wartości są ciągłe.

**PP-014: Prawdziwe dane to wymienna paczka.** Silnik musi działać na paczce fikcyjnej. Licencje na nazwy są problemem przyszłej publikacji, a nie architektury.

## Przejęte z Peloton Managera bez zmian

D-006 (postęp w dniach, sterowany zdarzeniami), D-007 (stabilne ID nigdy nieużywane ponownie), D-010 (AI bez wszechwiedzy), D-013 (zakres determinizmu), D-014 (prognozy nie zmieniają stanu i nie zużywają RNG), D-015 (kompaktowanie historii nie zmienia przyszłości), D-023–D-027 (Spy jako obowiązkowa infrastruktura, prawda debugowa nigdy nie staje się wiedzą w grze).

## Otwarte (czekają na decyzję)

Lista pytań jest w [ROADMAP.md](ROADMAP.md#otwarte-pytania).
