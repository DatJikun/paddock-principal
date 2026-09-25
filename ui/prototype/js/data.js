/* Dane przykładowe: alternatywny sezon 1976, gracz prowadzi Tyrrella.
   Wszystkie liczby to atrapa do prototypu wyglądu, nie wynik symulacji. */
window.DB = {
  date: { title: 'Środa, 7 lipca 1976', sub: 'runda 9 z 16 · Brands Hatch za 11 dni' },

  drivers: [
    { id:'scheckter', name:'Jody Scheckter', nat:'RSA', age:26, role:'Kierowca #1', no:3, stars:4, pot:4.5, form:'wysoka', morale:'dobre', trust:78,
      attrs:{zakr:17,ham:16,pl:13,wyp:15,obr:15,reg:14,opa:14,ada:15,desz:13,kon:16,inf:13},
      prefs:{balans:'lekka nadsterowność', trakcja:'ostra', hamowanie:'późne'}, traits:['Mistrz kwalifikacji','Twardy w obronie'],
      contract:{to:1976, salary:'£95 tys./rok', bonus:'£3 tys. za zwycięstwo'},
      clauses:['Może odejść, jeśli spadniemy poza top 3 konstruktorów','Status #1 gwarantowany','Zakaz startów w wyścigach sportowych'],
      promise:{text:'Lżejsza skrzynia biegów do GP Niemiec', left:'3 wyścigi', pct:62},
      career:{starts:52, wins:5, poles:3, podiums:17, points:124, titles:0},
      seasons:[['1974','Tyrrell',45,3],['1975','Tyrrell',20,1],['1976','Tyrrell',31,1]] },
    { id:'depailler', name:'Patrick Depailler', nat:'FRA', age:31, role:'Równy status', no:4, stars:3.5, pot:3.5, form:'dobra', morale:'bardzo dobre', trust:85,
      attrs:{zakr:16,ham:17,pl:14,wyp:14,obr:13,reg:12,opa:12,ada:14,desz:15,kon:14,inf:17},
      prefs:{balans:'neutralny', trakcja:'miękka', hamowanie:'późne'}, traits:['Znakomita informacja zwrotna'],
      contract:{to:1977, salary:'£60 tys./rok', bonus:'£2 tys. za podium'}, clauses:['Opcja przedłużenia po stronie zespołu (1978)'],
      promise:null, career:{starts:40, wins:0, poles:1, podiums:11, points:66, titles:0},
      seasons:[['1974','Tyrrell',14,0],['1975','Tyrrell',12,0],['1976','Tyrrell',20,0]] },
    { id:'hoffmann', name:'Ingo Hoffmann', nat:'BRA', age:23, role:'Kierowca testowy', no:'T', stars:2.5, pot:3, form:'—', morale:'dobre', trust:60,
      attrs:{zakr:13,ham:12,pl:13,wyp:11,obr:11,reg:12,opa:11,ada:13,desz:12,kon:14,inf:12},
      prefs:{balans:'neutralny', trakcja:'miękka', hamowanie:'normalne'}, traits:[],
      contract:{to:1976, salary:'£12 tys./rok', bonus:'—'}, clauses:[], promise:null,
      career:{starts:3, wins:0, poles:0, podiums:0, points:0, titles:0}, seasons:[['1976','Tyrrell',0,0]] },
  ],

  staff: [
    { name:'Derek Gardner', role:'Główny projektant', stars:4, attrs:{'Wizja':17,'Precyzja':16,'Innowacyjność':18}, contract:1977, note:'Autor P34' },
    { name:'Ken Tyrrell', role:'Właściciel', stars:4.5, attrs:{'Biznes':16,'Ludzie':18,'Polityka':15}, contract:'—', note:'Twój szef' },
    { name:'Roger Hill', role:'Szef mechaników', stars:3.5, attrs:{'Pit-stopy':14,'Jakość montażu':16,'Organizacja':15}, contract:1977, note:'' },
    { name:'Maurice Philippe', role:'Projektant', stars:3.5, attrs:{'Podwozie':15,'Integracja':14,'Precyzja':13}, contract:1978, note:'Przyszedł z Parnelli' },
    { name:'Tom Walsh', role:'Inżynier Schecktera', stars:3, attrs:{'Ustawienia':14,'Relacja':15,'Analiza':12}, contract:1976, note:'' },
    { name:'Pierre Morel', role:'Inżynier Depailliera', stars:3, attrs:{'Ustawienia':13,'Relacja':16,'Analiza':13}, contract:1977, note:'' },
  ],
  departments: [
    { name:'Biuro projektowe', people:6, quality:14, head:'Derek Gardner', cost:'£38 tys.' },
    { name:'Produkcja', people:14, quality:13, head:'Roger Hill', cost:'£52 tys.' },
    { name:'Zespół wyścigowy', people:11, quality:15, head:'Roger Hill', cost:'£44 tys.' },
    { name:'Dział komercyjny', people:2, quality:12, head:'Ken Tyrrell', cost:'£9 tys.' },
    { name:'Scouting', people:1, quality:11, head:'—', cost:'£4 tys.' },
  ],

  inbox: [
    { id:1, from:'Goodyear', av:'GY', title:'Umowa na lata 1977–79', when:'dziś', due:'do Pt 9 lip', decision:true, unread:true,
      body:'Goodyear chce przedłużyć umowę fabryczną na trzy sezony. W zamian proponują dalszy wspólny rozwój małych przednich opon 10″, bez których P34 traci sens. Warunek: wyłączność do końca 1979.',
      options:[{label:'Podpisz', plus:['Wspólny rozwój przednich opon','+£40 tys. rocznie'], minus:['Wyłączność do 1979']},
               {label:'Negocjuj', plus:['Szansa na krótszą umowę (2 lata)'], minus:['Ryzyko: Goodyear może się wycofać','Ok. 3 tygodnie rozmów']},
               {label:'Odmów', plus:['Wolny wybór dostawcy na 1977'], minus:['Koniec prac nad oponami 10″']}] },
    { id:2, from:'Derek Gardner', av:'DG', title:'Przednie opony zużywają się szybciej', when:'dziś', unread:true,
      body:'Dane z Paul Ricard pokazują, że małe przednie opony tracą przyczepność szybciej, niż zakładaliśmy. Na Brands Hatch stint może być krótszy o ok. 8 okrążeń. Strateg rozważa późniejszy postój, ale to zależy od temperatury.' },
    { id:3, from:'Skaut', av:'SK', title:'Didier Pironi, Formuła 2: raport', when:'dziś', unread:true,
      body:'Pironi (24 l.) jest najszybszym kierowcą w F2 w tym sezonie. Zakręty 14–17, opanowanie 12–16, cechy nieznane. Kontrakt w F2 do końca 1977. Warto obejrzeć go na żywo w Rouen.' },
    { id:4, from:'Jody Scheckter', av:'JS', title:'Pytanie o lżejszą skrzynię', when:'pon', unread:true,
      body:'Scheckter pyta, czy obietnica lżejszej skrzyni do GP Niemiec jest aktualna. Mówi wprost: jeśli jej nie będzie, zacznie rozmawiać z innymi zespołami o sezonie 1977.' },
    { id:5, from:'Ken Tyrrell', av:'KT', title:'Budżet na drugą połowę sezonu', when:'5 lip', unread:true,
      body:'Po sześciu wyścigach jesteśmy £30 tys. poniżej planu wydatków. Ken zgadza się przesunąć część na rozwój P34B, jeśli utrzymamy 2. miejsce wśród konstruktorów.' },
    { id:6, from:'Elf', av:'EL', title:'Kampania przed GP Wielkiej Brytanii', when:'3 lip', unread:false,
      body:'Elf przygotowuje kampanię z udziałem Schecktera. Potrzebują jednego dnia zdjęciowego przed wyścigiem.' },
    { id:7, from:'FIA', av:'FI', title:'Wyjaśnienie: wysokość wlotów powietrza', when:'28 cze', unread:false,
      body:'Przypomnienie: od GP Hiszpanii obowiązuje zakaz wysokich wlotów powietrza nad kokpitem. Kontrola technicza na Brands Hatch sprawdzi wymiary.' },
  ],

  calendar: [
    ['25 sty','Brazylia','Interlagos','Lauda','P3 / P7'],['6 mar','RPA','Kyalami','Lauda','P2 / DNF'],['28 mar','USA Zachód','Long Beach','Regazzoni','P4 / P5'],
    ['2 maj','Hiszpania','Jarama','Hunt','P3 / DNF'],['16 maj','Belgia','Zolder','Lauda','P4 / P6'],['30 maj','Monako','Monte Carlo','Lauda','P2 / P3'],
    ['13 cze','Szwecja','Anderstorp','Scheckter','P1 / P2'],['4 lip','Francja','Paul Ricard','Hunt','P6 / P2'],
    ['18 lip','Wielka Brytania','Brands Hatch',null,null],['1 sie','Niemcy','Nürburgring',null,null],['15 sie','Austria','Österreichring',null,null],
    ['29 sie','Holandia','Zandvoort',null,null],['12 wrz','Włochy','Monza',null,null],['3 paź','Kanada','Mosport',null,null],
    ['10 paź','USA Wschód','Watkins Glen',null,null],['24 paź','Japonia','Fuji',null,null],
  ],

  standings: {
    drivers:[['Niki Lauda','Ferrari',43],['Jody Scheckter','Tyrrell',31,1],['James Hunt','McLaren',26],['Patrick Depailler','Tyrrell',20,1],['Clay Regazzoni','Ferrari',16],
      ['Jochen Mass','McLaren',12],['Gunnar Nilsson','Lotus',9],['Ronnie Peterson','March',7],['Jacques Laffite','Ligier',7],['John Watson','Penske',6],['Mario Andretti','Lotus',4],['Carlos Reutemann','Brabham',3]],
    constructors:[['Ferrari',51],['Tyrrell-Ford',42,1],['McLaren-Ford',30],['Lotus-Ford',13],['Penske-Ford',10],['Ligier-Matra',9],['March-Ford',8],['Brabham-Alfa Romeo',3]],
  },

  car: {
    name:'Tyrrell P34', concept:'Sześć kół, małe przednie koła 10″',
    areas:[['Przyczepność mechaniczna',1],['Hamowanie',2],['Docisk',6],['Niezawodność',8],['Moc',10]],
    axes:[['Aero','mały opór','duży docisk',62],['Filozofia','ewolucja','rewolucja',88],['Okno pracy','szerokie','wąskie',70],['Chłodzenie','zapas','na krawędzi',45],['Opony','łagodne','agresywne',58]],
    projects:[{name:'Przednie zawieszenie', stream:'Bieżące auto', pct:62, eta:'3 tyg.', note:'Przyczepność mech. i przednie opony'},
              {name:'Lżejsza skrzynia biegów', stream:'Bieżące auto', pct:35, eta:'5 tyg.', note:'Obietnica dla Schecktera'},
              {name:'Koncepcja 1977: P34B', stream:'Przyszły rok', pct:28, eta:'zamrożenie za 5 tyg.', note:'Zapas koncepcji: 4–7 pkt (szacunek Gardnera, pewność średnia)'}],
    bank:'+1,8 pkt', split:[60,15,25],
    understanding:[['Podwozie P34/2',92],['Przednie skrzydło B',74],['Nowe tylne zawieszenie',41]],
  },

  facilities: [
    { name:'Fabryka w Ockham', level:'Warsztat', frontier:62, note:'Dobra jak na zespół prywatny', cost:null },
    { name:'Tunel aerodynamiczny', level:'Wynajem (Imperial College)', frontier:35, note:'Własny tunel: £180 tys., 14 mies.', cost:'£180 tys.' },
    { name:'Hamownia', level:'Brak', frontier:0, note:'Silniki od Cosworth, niepotrzebna przy kliencie', cost:'£60 tys.' },
    { name:'Warsztat kompozytów', level:'Podstawowy', frontier:48, note:'Nowa technologia; liderzy dopiero zaczynają', cost:'£45 tys.' },
    { name:'Biuro projektowe', level:'6 desek kreślarskich', frontier:58, note:'Rozbudowa: +2 stanowiska', cost:'£12 tys.' },
  ],

  suppliers: [
    { cat:'Silnik', name:'Ford Cosworth DFV', type:'Klient', to:1977, pros:'Niezawodny, sprawdzony', cons:'Taki sam jak u 9 rywali' },
    { cat:'Opony', name:'Goodyear', type:'Fabryczna', to:1976, pros:'Specjalne przednie 10″', cons:'Umowa kończy się w tym roku' },
    { cat:'Paliwo i olej', name:'Elf', type:'Partner', to:1978, pros:'Pieniądze + paliwo za darmo', cons:'Obowiązki marketingowe' },
    { cat:'Skrzynia biegów', name:'Hewland', type:'Klient', to:1976, pros:'Standard stawki', cons:'Ciężka jak na P34' },
    { cat:'Hamulce', name:'Lockheed', type:'Klient', to:1977, pros:'Dobre chłodzenie', cons:'—' },
  ],

  sponsors: [
    { slot:'Tytularny', name:'Elf', ind:'Paliwa', amount:'£180 tys./rok', to:1978, goal:'Top 3 konstruktorów', goalOk:true },
    { slot:'Główny', name:'First National City Travelers Checks', ind:'Finanse', amount:'£70 tys./rok', to:1977, goal:'Zwycięstwo w sezonie', goalOk:true },
    { slot:'Mniejszy', name:'Goodyear', ind:'Opony', amount:'£15 tys./rok', to:1976, goal:'—', goalOk:true },
    { slot:'Mniejszy', name:'—', ind:'wolne miejsce', amount:'—', to:null, goal:'4 firmy zainteresowane', goalOk:null },
  ],

  finance: {
    income:[['Sponsorzy',265],['Pieniądze startowe',120],['Nagrody',38],['Sprzedaż części',6]],
    costs:[['Pensje kierowców',167],['Personel',97],['Silniki i części',118],['Rozwój',64],['Podróże',41],['Infrastruktura',18]],
    months:[[-12,'sty'],[18,'lut'],[-8,'mar'],[22,'kwi'],[15,'maj'],[31,'cze'],[-6,'lip'],[null,'sie'],[null,'wrz'],[null,'paź']],
  },

  board: {
    owner:'Ken Tyrrell', mood:'Zadowolony', patience:'Wysoka (lojalny wobec swoich ludzi)', rep:68,
    goals:[['Top 3 konstruktorów w 1976',true,'2. miejsce'],['Zwycięstwo w wyścigu',true,'Szwecja'],['Budżet bez deficytu',true,'+£30 tys.'],['Utrzymać Schecktera na 1977',null,'zależy od obietnicy']],
  },

  market: [
    ['Ronnie Peterson','SWE',32,'March',4,4,'1976','zainteresowany'],['Mario Andretti','USA',36,'Lotus',4,4,'1977','raczej nie'],
    ['John Watson','GBR',30,'Penske',3.5,3.5,'1976','zainteresowany'],['Jacques Laffite','FRA',32,'Ligier',3.5,3.5,'1977','raczej nie'],
    ['Didier Pironi','FRA',24,'F2',3,4.5,'1977','bardzo zainteresowany'],['Gilles Villeneuve','CAN',26,'Atlantic',3,5,'1976','bardzo zainteresowany'],
    ['Jean-Pierre Jarier','FRA',29,'Shadow',3,3,'1976','zainteresowany'],['Vittorio Brambilla','ITA',38,'March',3,3,'1976','zainteresowany'],
    ['Hans-Joachim Stuck','GER',25,'March',3,3.5,'1977','neutralny'],['Carlos Reutemann','ARG',34,'Brabham',4,4,'1976','neutralny'],
    ['Alan Jones','AUS',29,'Surtees',3,3.5,'1976','zainteresowany'],['Riccardo Patrese','ITA',22,'F3',2.5,4,'1976','bardzo zainteresowany'],
    ['Eddie Cheever','USA',18,'F3',2,4,'1977','bardzo zainteresowany'],['Emerson Fittipaldi','BRA',29,'Copersucar',3.5,3.5,'1977','raczej nie'],
    ['Tom Pryce','GBR',26,'Shadow',3,3.5,'1976','zainteresowany'],['Arturo Merzario','ITA',33,'Williams',2.5,2.5,'1976','zainteresowany'],
  ],

  academy: [
    { name:'Derek Daly', nat:'IRL', age:23, series:'Formuła Ford', stars:2, pot:3.5, program:'Opłacony sezon w F3 1977', progress:40 },
    { name:'Stephen South', nat:'GBR', age:23, series:'F3', stars:2.5, pot:3.5, program:'Testy w Silverstone', progress:65 },
  ],

  monthly: {
    issue:'nr 7 · lipiec 1976',
    lead:{sec:'Rynek', title:'Peterson rozmawia z Tyrrellem?', text:'Szwed ma dość Marcha. Jego ludzie pytają o miejsce na 1977, jeśli Scheckter odejdzie. W padoku mówi się też o zainteresowaniu Lotusa, ale Chapman ma już Andrettiego i Nilssona.', link:'Plotki transferowe'},
    stories:[
      {sec:'Le Mans 24h', title:'Porsche 936 wygrywa, Ickx i van Lennep na szczycie', text:'Renault prowadziło przez noc, ale turbo nie wytrzymało. Porsche umacnia się na prowadzeniu w Mistrzostwach Świata Samochodów Sportowych.', link:'Klasyfikacja mistrzostw'},
      {sec:'Łowcy talentów', title:'Villeneuve rozbija Formułę Atlantic', text:'Kanadyjczyk wygrał pięć z sześciu wyścigów. Kto pierwszy da mu test w F1?', link:'Profil kierowcy'},
      {sec:'Technika', title:'Ferrari testuje nowy tylny spojler', text:'Na Fiorano widziano 312T2 z szerszym tylnym skrzydłem. Nasi ludzie szacują zysk na szybkich torach.', link:'Co wiemy o 312T2'},
      {sec:'Pieniądze', title:'Tytoń kupuje F1', text:'Kolejne zespoły zmieniają barwy dla sponsorów tytoniowych. Ile to jest warte i co będzie, gdy przyjdzie zakaz?', link:'Rynek sponsorów'},
      {sec:'Z historii', title:'Dziesięć lat temu: Brabham mistrzem we własnym aucie', text:'Jedyny taki przypadek w historii. Czy ktoś to powtórzy?', link:'Kronika'},
    ],
  },

  fia: {
    rules:[['Silnik','3,0 l wolnossący lub 1,5 l turbo'],['Masa minimalna','575 kg'],['Punkty','9-6-4-3-2-1'],['Liczone wyniki','7 najlepszych z pierwszej połowy + 7 z drugiej'],['Tankowanie','dozwolone (nikt nie korzysta)'],['Wloty powietrza','zakaz wysokich wlotów od GP Hiszpanii']],
    proposals:[{title:'Limit szerokości tylnych opon', from:'propozycja: CSI', effect:'−3% docisku mechanicznego dla wszystkich', vote:'głosowanie w październiku', stance:null},
               {title:'Obowiązkowa gaśnica pokładowa', from:'propozycja: GPDA', effect:'+4 kg masy, lepsze bezpieczeństwo', vote:'wchodzi od 1977', stance:'za'}],
  },

  chronicle: [
    ['13 cze 1976','Scheckter wygrywa w Szwecji, dublet Tyrrella','Tak jak w rzeczywistości.'],
    ['4 lip 1976','Hunt wygrywa we Francji, Lauda dopiero piąty','W rzeczywistości Hunt też wygrał, ale Lauda nie ukończył.'],
    ['Styczeń 1976','Przejmujesz Tyrrella po Kenie jako szefie zespołu','W rzeczywistości Ken Tyrrell prowadził zespół sam do 1998.'],
  ],
};
