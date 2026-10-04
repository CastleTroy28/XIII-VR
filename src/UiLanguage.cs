using System;
namespace XiiiXR;
internal static class UiLanguage
{
    internal static Func<string>? ReadCode=null;
    internal static Func<bool>? ReadManual=null;
    internal static Action<bool>? WriteManual=null;
    internal static string Code { get { try{return (ReadCode?.Invoke()??"ru").Split('-','_')[0].ToLowerInvariant();}catch{return "en";} } }
    internal static bool Russian=>Code=="ru";
    internal static string T(string russian,string english)=>Russian?russian:english;
    // 0.1.103: every text the mod shows follows the game's language
    // (I2 CurrentLanguageCode). English source → ru, de, fr, es, it, pl, pt.
    private static readonly string[] Languages={"ru","de","fr","es","it","pl","pt"};
    private static readonly System.Collections.Generic.Dictionary<string,string[]> Table=new()
    {
        ["Release left Grip"]=new[]{"Отпустить левый Grip","Linken Grip loslassen","Relâcher le Grip gauche","Suelta el Grip izquierdo","Rilascia il Grip sinistro","Puść lewy Grip","Solte o Grip esquerdo"},
        ["Hold right Grip"]=new[]{"Удерживать правый Grip","Rechten Grip halten","Maintenir le Grip droit","Mantén el Grip derecho","Tieni premuto il Grip destro","Przytrzymaj prawy Grip","Segure o Grip direito"},
        ["Hold left Grip"]=new[]{"Удерживать левый Grip","Linken Grip halten","Maintenir le Grip gauche","Mantén el Grip izquierdo","Tieni premuto il Grip sinistro","Przytrzymaj lewy Grip","Segure o Grip esquerdo"},
        ["Right stick: swing · L3: let go"]=new[]{"Правый стик: раскачка · L3: отцепиться","Rechter Stick: schwingen · L3: loslassen","Stick droit : se balancer · L3 : lâcher","Stick derecho: balancearse · L3: soltarse","Stick destro: oscilla · L3: sganciati","Prawy drążek: huśtanie · L3: puść","Analógico direito: balançar · L3: soltar"},
        ["L3 (or right B): let go"]=new[]{"L3 (или правая B): отцепиться","L3 (oder rechts B): loslassen","L3 (ou B droit) : lâcher","L3 (o B derecho): soltarse","L3 (o B destro): sganciati","L3 (lub prawy B): puść","L3 (ou B direito): soltar"},
        ["Left stick: swing · R3: let go"]=new[]{"Левый стик: раскачка · R3: отцепиться","Linker Stick: schwingen · R3: loslassen","Stick gauche : se balancer · R3 : lâcher","Stick izquierdo: balancearse · R3: soltarse","Stick sinistro: oscilla · R3: sganciati","Lewy drążek: huśtanie · R3: puść","Analógico esquerdo: balançar · R3: soltar"},
        ["R3: let go"]=new[]{"R3: отцепиться","R3: loslassen","R3 : lâcher","R3: soltarse","R3: sganciati","R3: puść","R3: soltar"},
        ["Right stick up/down"]=new[]{"Правый стик вверх/вниз","Rechter Stick hoch/runter","Stick droit haut/bas","Stick derecho arriba/abajo","Stick destro su/giù","Prawy drążek góra/dół","Analógico direito cima/baixo"},
        ["Left trigger (hook in left hand)"]=new[]{"Левый триггер (кошка в левой руке)","Linker Trigger (Haken in der linken Hand)","Gâchette gauche (grappin en main gauche)","Gatillo izquierdo (gancho en la mano izquierda)","Grilletto sinistro (rampino nella mano sinistra)","Lewy spust (hak w lewej ręce)","Gatilho esquerdo (gancho na mão esquerda)"},
        // 0.1.195: the grappling hook in the right hand; the zipline hook.
        ["Right trigger (hook in right hand)"]=new[]{"Правый триггер (кошка в правой руке)","Rechter Trigger (Haken in der rechten Hand)","Gâchette droite (grappin en main droite)","Gatillo derecho (gancho en la mano derecha)","Grilletto destro (rampino nella mano destra)","Prawy spust (hak w prawej ręce)","Gatilho direito (gancho na mão direita)"},
        ["Point the zipline hook at the cable"]=new[]{"Наведите крюк на трос","Den Seilrutschen-Haken auf das Seil richten","Pointez le crochet de tyrolienne vers le câble","Apunta el gancho de tirolina al cable","Punta il gancio della teleferica sul cavo","Skieruj hak tyrolki na linę","Aponte o gancho de tirolesa para o cabo"},
        ["Wheel: zipline hook, point it at the cable"]=new[]{"Колесо: крюк для троса, наведите на трос","Rad: Seilrutschen-Haken, auf das Seil richten","Roue : crochet de tyrolienne, pointez-le vers le câble","Rueda: gancho de tirolina, apúntalo al cable","Ruota: gancio della teleferica, puntalo sul cavo","Koło: hak tyrolki, skieruj na linę","Roda: gancho de tirolesa, aponte para o cabo"},
        ["Left stick down"]=new[]{"Левый стик вниз","Linker Stick runter","Stick gauche vers le bas","Stick izquierdo abajo","Stick sinistro giù","Lewy drążek w dół","Analógico esquerdo para baixo"},
        ["Left stick up"]=new[]{"Левый стик вверх","Linker Stick hoch","Stick gauche vers le haut","Stick izquierdo arriba","Stick sinistro su","Lewy drążek w górę","Analógico esquerdo para cima"},
        ["Left stick up/down"]=new[]{"Левый стик вверх/вниз","Linker Stick hoch/runter","Stick gauche haut/bas","Stick izquierdo arriba/abajo","Stick sinistro su/giù","Lewy drążek góra/dół","Analógico esquerdo cima/baixo"},
        ["Left stick"]=new[]{"Левый стик","Linker Stick","Stick gauche","Stick izquierdo","Stick sinistro","Lewy drążek","Analógico esquerdo"},
        ["Left stick click"]=new[]{"Клик левого стика","Linken Stick drücken","Clic du stick gauche","Pulsar stick izquierdo","Premi lo stick sinistro","Wciśnij lewy drążek","Pressione o analógico esquerdo"},
        ["Right stick up"]=new[]{"Правый стик вверх","Rechter Stick hoch","Stick droit vers le haut","Stick derecho arriba","Stick destro su","Prawy drążek w górę","Analógico direito para cima"},
        ["Right stick down"]=new[]{"Правый стик вниз","Rechter Stick runter","Stick droit vers le bas","Stick derecho abajo","Stick destro giù","Prawy drążek w dół","Analógico direito para baixo"},
        ["Left stick or arm strokes"]=new[]{"Левый стик или гребки руками","Linker Stick oder Armzüge","Stick gauche ou brasses","Stick izquierdo o brazadas","Stick sinistro o bracciate","Lewy drążek lub ruchy ramion","Analógico esquerdo ou braçadas"},
        ["Right stick"]=new[]{"Правый стик","Rechter Stick","Stick droit","Stick derecho","Stick destro","Prawy drążek","Analógico direito"},
        ["Head movement"]=new[]{"Движение головы","Kopfbewegung","Mouvement de la tête","Movimiento de la cabeza","Movimento della testa","Ruch głowy","Movimento da cabeça"},
        ["Right Grip + A"]=new[]{"Правый Grip + A","Rechter Grip + A","Grip droit + A","Grip derecho + A","Grip destro + A","Prawy Grip + A","Grip direito + A"},
        ["Left Grip"]=new[]{"Левый Grip","Linker Grip","Grip gauche","Grip izquierdo","Grip sinistro","Lewy Grip","Grip esquerdo"},
        ["Left trigger"]=new[]{"Левый триггер","Linker Trigger","Gâchette gauche","Gatillo izquierdo","Grilletto sinistro","Lewy spust","Gatilho esquerdo"},
        ["Right Grip: support weapon"]=new[]{"Правый Grip: двуручный хват","Rechter Grip: Waffe stützen","Grip droit : tenir à deux mains","Grip derecho: sujetar a dos manos","Grip destro: impugnatura a due mani","Prawy Grip: chwyt oburącz","Grip direito: segurar com as duas mãos"},
        ["Right Grip"]=new[]{"Правый Grip","Rechter Grip","Grip droit","Grip derecho","Grip destro","Prawy Grip","Grip direito"},
        ["Fist in the back, swing hard"]=new[]{"Кулаком в спину с размаху","Faust in den Rücken, kräftig ausholen","Poing dans le dos, avec élan","Puño en la espalda, con fuerza","Pugno nella schiena, con forza","Pięścią w plecy, z zamachem","Soco nas costas, com força"},
        ["Left/Right Grip"]=new[]{"Левый/правый Grip","Linker/rechter Grip","Grip gauche/droit","Grip izquierdo/derecho","Grip sinistro/destro","Lewy/prawy Grip","Grip esquerdo/direito"},
        ["Left X"]=new[]{"X на левом контроллере","Links X","X gauche","X izquierdo","X sinistro","Lewy X","X esquerdo"},
        ["Right trigger"]=new[]{"Правый триггер","Rechter Trigger","Gâchette droite","Gatillo derecho","Grilletto destro","Prawy spust","Gatilho direito"},
        ["Right stick click"]=new[]{"Клик правого стика","Rechten Stick drücken","Clic du stick droit","Pulsar stick derecho","Premi lo stick destro","Wciśnij prawy drążek","Pressione o analógico direito"},
        ["Right B"]=new[]{"B на правом контроллере","Rechts B","B droit","B derecho","B destro","Prawy B","B direito"},
        ["Left Grip: support weapon"]=new[]{"Левый Grip: двуручный хват","Linker Grip: Waffe stützen","Grip gauche : tenir à deux mains","Grip izquierdo: sujetar a dos manos","Grip sinistro: impugnatura a due mani","Lewy Grip: chwyt oburącz","Grip esquerdo: segurar com as duas mãos"},
        ["A: wheel, left stick"]=new[]{"A: селектор, левый стик","A: Rad, linker Stick","A : roue, stick gauche","A: rueda, stick izquierdo","A: ruota, stick sinistro","A: koło, lewy drążek","A: roda, analógico esquerdo"},
        ["Grip + swing"]=new[]{"Grip + замах","Grip + Ausholen","Grip + élan","Grip + golpe","Grip + colpo","Grip + zamach","Grip + golpe"},
        ["Left Y"]=new[]{"Y на левом контроллере","Links Y","Y gauche","Y izquierdo","Y sinistro","Lewy Y","Y esquerdo"},
        ["Right A: wheel, left stick"]=new[]{"Правый A: селектор, левый стик","Rechts A: Rad, linker Stick","A droit : roue, stick gauche","A derecho: rueda, stick izquierdo","A destro: ruota, stick sinistro","Prawy A: koło, lewy drążek","A direito: roda, analógico esquerdo"},
        ["Hold right A"]=new[]{"Удерживать правый A","Rechts A halten","Maintenir A droit","Mantén A derecho","Tieni premuto A destro","Przytrzymaj prawy A","Segure A direito"},
        ["A: wheel → medkit → trigger"]=new[]{"A: селектор → аптечка → триггер","A: Rad → Medikit → Trigger","A : roue → trousse de soins → gâchette","A: rueda → botiquín → gatillo","A: ruota → medikit → grilletto","A: koło → apteczka → spust","A: roda → kit médico → gatilho"},
        ["Right A"]=new[]{"A на правом контроллере","Rechts A","A droit","A derecho","A destro","Prawy A","A direito"},
        ["Left Grip + X"]=new[]{"Левый Grip + X","Linker Grip + X","Grip gauche + X","Grip izquierdo + X","Grip sinistro + X","Lewy Grip + X","Grip esquerdo + X"},
        ["Hold card to reader"]=new[]{"Приложите карту","Karte an den Leser halten","Approchez la carte du lecteur","Acerca la tarjeta al lector","Avvicina la tessera al lettore","Przyłóż kartę do czytnika","Encoste o cartão no leitor"},
        ["Turn key"]=new[]{"Поверните ключ","Schlüssel drehen","Tournez la clé","Gira la llave","Gira la chiave","Przekręć klucz","Gire a chave"},
        ["Turn lockpick"]=new[]{"Поверните отмычку","Dietrich drehen","Tournez le crochet","Gira la ganzúa","Gira il grimaldello","Przekręć wytrych","Gire a gazua"},
        ["Hold lockpick in lock"]=new[]{"Держите отмычку в замке","Dietrich im Schloss halten","Gardez le crochet dans la serrure","Mantén la ganzúa en la cerradura","Tieni il grimaldello nella serratura","Trzymaj wytrych w zamku","Mantenha a gazua na fechadura"},
        ["No current objectives"]=new[]{"Нет текущих задач","Keine aktuellen Ziele","Aucun objectif en cours","No hay objetivos actuales","Nessun obiettivo attuale","Brak bieżących celów","Nenhum objetivo atual"},
        ["OBJECTIVES"]=new[]{"ЗАДАЧИ","ZIELE","OBJECTIFS","OBJETIVOS","OBIETTIVI","CELE","OBJETIVOS"},
        ["X (left controller) — close"]=new[]{"X (левый контроллер) — закрыть","X (linker Controller) — schließen","X (manette gauche) — fermer","X (mando izquierdo) — cerrar","X (controller sinistro) — chiudi","X (lewy kontroler) — zamknij","X (controle esquerdo) — fechar"},
        ["VR SETTINGS"]=new[]{"НАСТРОЙКИ VR","VR-EINSTELLUNGEN","PARAMÈTRES VR","AJUSTES VR","IMPOSTAZIONI VR","USTAWIENIA VR","CONFIGURAÇÕES VR"},
        // 0.1.107: VR death screen.
        ["YOUR CHARACTER HAS DIED"]=new[]{"ВАШ ПЕРСОНАЖ УМЕР","DEINE FIGUR IST GESTORBEN","VOTRE PERSONNAGE EST MORT","TU PERSONAJE HA MUERTO","IL TUO PERSONAGGIO È MORTO","TWOJA POSTAĆ ZGINĘŁA","SEU PERSONAGEM MORREU"},
        ["OBJECTIVE FAILED"]=new[]{"ЗАДАНИЕ ПРОВАЛЕНО","ZIEL VERFEHLT","OBJECTIF ÉCHOUÉ","OBJETIVO FALLIDO","OBIETTIVO FALLITO","CEL NIEOSIĄGNIĘTY","OBJETIVO FALHADO"},
        ["CONTINUE"]=new[]{"ПРОДОЛЖИТЬ","WEITER","CONTINUER","CONTINUAR","CONTINUA","KONTYNUUJ","CONTINUAR"},
        ["QUIT"]=new[]{"ВЫЙТИ","BEENDEN","QUITTER","SALIR","ESCI","WYJDŹ","SAIR"},
        ["Resolution"]=new[]{"Разрешение","Auflösung","Résolution","Resolución","Risoluzione","Rozdzielczość","Resolução"},
        ["Collisions"]=new[]{"Коллизии","Kollisionen","Collisions","Colisiones","Collisioni","Kolizje","Colisões"},
        ["Movement"]=new[]{"Передвижение","Fortbewegung","Déplacement","Movimiento","Movimento","Ruch","Movimento"},
        ["Turning"]=new[]{"Повороты","Drehung","Rotation","Giro","Rotazione","Obrót","Rotação"},
        ["Angle"]=new[]{"Угол","Winkel","Angle","Ángulo","Angolo","Kąt","Ângulo"},
        ["Speed"]=new[]{"Скорость","Geschwindigkeit","Vitesse","Velocidad","Velocità","Prędkość","Velocidade"},
        ["°/s"]=new[]{"°/с","°/s","°/s","°/s","°/s","°/s","°/s"},
        ["Manual reload"]=new[]{"Ручная перезарядка","Manuelles Nachladen","Rechargement manuel","Recarga manual","Ricarica manuale","Ręczne przeładowanie","Recarga manual"},
        ["Recenter"]=new[]{"Центровка","Zentrieren","Recentrer","Centrar","Ricentra","Wycentruj","Centralizar"},
        ["Reset hands"]=new[]{"Сброс положения рук","Hände zurücksetzen","Réinitialiser les mains","Restablecer manos","Ripristina mani","Resetuj ręce","Redefinir mãos"},
        ["Calibrate right hand (3 s)"]=new[]{"Калибровка правой руки (3 с)","Rechte Hand kalibrieren (3 s)","Calibrer la main droite (3 s)","Calibrar mano derecha (3 s)","Calibra mano destra (3 s)","Kalibruj prawą rękę (3 s)","Calibrar mão direita (3 s)"},
        ["Test vibration"]=new[]{"Проверка вибрации","Vibration testen","Tester la vibration","Probar vibración","Prova vibrazione","Test wibracji","Testar vibração"},
        ["Interaction hints"]=new[]{"Подсказки","Interaktionshinweise","Aides d'interaction","Indicaciones","Suggerimenti","Podpowiedzi","Dicas de interação"},
        // 0.1.117: manual reload stages on the right watch (watch glyphs:
        // upper case, no accents).
        ["DROP"]=new[]{"СБРОС","RAUS","LACHER","SOLTAR","SGANCIA","WYRZUC","SOLTAR"},
        ["INSERT"]=new[]{"ВСТАВЬ","REIN","INSERER","INSERTAR","INSERISCI","WLOZ","INSERIR"},
        ["GRAB"]=new[]{"ВЗЯТЬ","GREIFEN","PRENDRE","TOMAR","PRENDI","CHWYC","PEGAR"},
        ["POUCH"]=new[]{"ПОЯС","GURTEL","CEINTURE","CINTURON","CINTURA","PAS","CINTO"},
        ["COVER"]=new[]{"КРЫШКА","DECKEL","CAPOT","TAPA","COPERCHIO","POKRYWA","TAMPA"},
        ["RACK"]=new[]{"ЗАТВОР","SPANNEN","ARMER","CERROJO","ARMARE","ZAMEK","ENGATILHAR"},
        // 0.1.183: a pistol's magazine out, both hands full: strike the grip against the chest.
        ["CHEST"]=new[]{"ГРУДЬ","BRUST","POITRINE","PECHO","PETTO","PIERS","PEITO"},
        // 0.1.194: the double-barrelled shotgun: open it (B/Y), shut it (a flick).
        ["OPEN"]=new[]{"ОТКРОЙ","OFFNEN","OUVRIR","ABRIR","APRI","OTWORZ","ABRIR"},
        ["CLOSE"]=new[]{"ЗАКРОЙ","SCHLIESSEN","FERMER","CERRAR","CHIUDI","ZAMKNIJ","FECHAR"},
        // 0.1.118: grenade by hand.
        ["Left trigger at the grenade: pin · hold right trigger, swing, let go"]=new[]{"Левый триггер у гранаты: чека · держите правый триггер, замах, отпустите","Linker Trigger an der Granate: Sicherungsstift · rechten Trigger halten, ausholen, loslassen","Gâchette gauche sur la grenade : goupille · maintenez la gâchette droite, lancez, relâchez","Gatillo izquierdo en la granada: anilla · mantén el gatillo derecho, lanza y suelta","Grilletto sinistro sulla granata: sicura · tieni premuto il grilletto destro, lancia, rilascia","Lewy spust przy granacie: zawleczka · trzymaj prawy spust, zamach, puść","Gatilho esquerdo na granada: pino · segure o gatilho direito, arremesse e solte"},
        // 0.1.149: throwable things held by the grip, thrown by letting go in a swing.
        ["Grip: hold · swing and let go: throw"]=new[]{"Grip: держать · замах и отпустить: бросок","Grip: halten · ausholen und loslassen: werfen","Grip : tenir · élan et relâcher : lancer","Grip: sujetar · impulso y soltar: lanzar","Grip: tieni · slancio e rilascia: lancia","Grip: trzymaj · zamach i puść: rzut","Grip: segurar · balançar e soltar: arremessar"},
        ["Left trigger at the grenade: pin · swing and let go of the grip"]=new[]{"Левый триггер у гранаты: чека · замах и отпустите Grip","Linker Trigger an der Granate: Sicherungsstift · ausholen und Grip loslassen","Gâchette gauche sur la grenade : goupille · élan et relâchez le Grip","Gatillo izquierdo en la granada: anilla · impulso y suelta el Grip","Grilletto sinistro sulla granata: sicura · slancio e rilascia il Grip","Lewy spust przy granacie: zawleczka · zamach i puść Grip","Gatilho esquerdo na granada: pino · balance e solte o Grip"},
        // 0.1.119: underbarrel grenades after the rounds on the watch.
        ["Left trigger while holding the fore-end"]=new[]{"Левый триггер, держа цевьё","Linker Trigger, während Sie den Vorderschaft halten","Gâchette gauche en tenant le garde-main","Gatillo izquierdo sujetando el guardamanos","Grilletto sinistro tenendo l'astina","Lewy spust, trzymając łoże","Gatilho esquerdo segurando o guarda-mão"},
        ["Both Grips"]=new[]{"Оба Grip","Beide Grips","Les deux Grips","Ambos Grips","Entrambi i Grip","Oba Gripy","Ambos os Grips"},
        ["Release both Grips"]=new[]{"Отпустите оба Grip","Beide Grips loslassen","Relâchez les deux Grips","Suelta ambos Grips","Rilascia entrambi i Grip","Puść oba Gripy","Solte ambos os Grips"},
        ["GL"]=new[]{"Г","G","G","G","G","G","G"},
        // 0.1.120: stationary machine gun steering.
        ["Weapon in hand"]=new[]{"Оружие в руке","Waffe in der Hand","Arme en main","Arma en la mano","Arma in mano","Broń w ręce","Arma na mão"},
        ["hold grip"]=new[]{"пока держишь грипп","Grip halten","maintenir le Grip","mantener el Grip","tenere il Grip","trzymaj Grip","segurar o Grip"},
        ["grip press (take/let go)"]=new[]{"нажатие гриппа (взять/отпустить)","Grip drücken (nehmen/loslassen)","appui Grip (prendre/lâcher)","pulsar Grip (coger/soltar)","premere Grip (prendi/lascia)","naciśnij Grip (weź/puść)","pressionar Grip (pegar/soltar)"},
        ["always"]=new[]{"всегда","immer","toujours","siempre","sempre","zawsze","sempre"},
        ["Dominant hand"]=new[]{"Ведущая рука","Starke Hand","Main dominante","Mano dominante","Mano dominante","Ręka wiodąca","Mão dominante"},
        ["right-handed"]=new[]{"правша","Rechtshänder","droitier","diestro","destrimano","praworęczny","destro"},
        ["left-handed"]=new[]{"левша","Linkshänder","gaucher","zurdo","mancino","leworęczny","canhoto"},
        ["Pistols on the belt"]=new[]{"Пистолеты на поясе","Pistolen am Gürtel","Pistolets à la ceinture","Pistolas en el cinturón","Pistole alla cintura","Pistolety przy pasie","Pistolas no cinto"},
        ["Pistols under the arms"]=new[]{"Пистолеты под мышками","Pistolen unter den Armen","Pistolets sous les bras","Pistolas bajo los brazos","Pistole sotto le braccia","Pistolety pod pachami","Pistolas sob os braços"},
        ["Mounted gun"]=new[]{"Стационарный пулемёт","Stationäres MG","Mitrailleuse fixe","Ametralladora fija","Mitragliatrice fissa","Karabin stacjonarny","Metralhadora fixa"},
        ["handles (inverted)"]=new[]{"рукоятки (инверсия)","Griffe (invertiert)","poignées (inversé)","empuñaduras (invertido)","impugnature (invertito)","rękojeści (odwrócone)","empunhaduras (invertido)"},
        ["pointing"]=new[]{"по направлению рук","Zeigen","pointage","apuntar","puntamento","wskazywanie","apontar"},
        ["Throwing"]=new[]{"Броски","Werfen","Lancer","Lanzamiento","Lancio","Rzucanie","Arremesso"},
        ["Close"]=new[]{"Закрыть","Schließen","Fermer","Cerrar","Chiudi","Zamknij","Fechar"},
        ["Two-handed gun in one hand"]=new[]{"Двуручное в одной руке","Zweihandwaffe in einer Hand","Arme à deux mains d'une main","Arma de dos manos en una mano","Arma a due mani in una mano","Broń dwuręczna w jednej ręce","Arma de duas mãos numa mão"},
        ["toward the other hand"]=new[]{"к другой руке","zur anderen Hand","vers l'autre main","hacia la otra mano","verso l'altra mano","w stronę drugiej ręki","para a outra mão"},
        ["straight"]=new[]{"прямо","gerade","droit","recta","dritta","prosto","reta"},
        ["on"]=new[]{"вкл","an","oui","sí","sì","tak","sim"},
        ["off"]=new[]{"выкл","aus","non","no","no","nie","não"},
        ["teleport"]=new[]{"телепорт","Teleport","téléportation","teletransporte","teletrasporto","teleport","teletransporte"},
        ["slide"]=new[]{"скольжение","Gleiten","continu","continuo","continuo","ciągły","contínuo"},
        ["snap"]=new[]{"ступенчатые (snap)","Stufen","par crans","por pasos","a scatti","skokowy","por etapas"},
        ["smooth"]=new[]{"плавные (smooth)","fließend","fluide","suave","fluida","płynny","suave"},
        ["hold + flight path"]=new[]{"удержание + траектория","halten + Flugbahn","maintien + trajectoire","mantener + trayectoria","tieni + traiettoria","przytrzymaj + tor lotu","segurar + trajetória"},
        ["gesture (draw back, snap)"]=new[]{"жест (замах, рывок)","Geste (ausholen, schnellen)","geste (armer, lancer)","gesto (atrás, impulso)","gesto (carica, scatto)","gest (zamach, wyrzut)","gesto (recuar, impulso)"},
        ["per eye"]=new[]{"на глаз","pro Auge","par œil","por ojo","per occhio","na oko","por olho"},
        ["Right controller: point and pull the trigger (‹ › adjust)."]=new[]{"Правый контроллер: наведите и нажмите триггер (‹ › — изменить).","Rechter Controller: zielen und Trigger drücken (‹ › ändern).","Manette droite : pointez et appuyez sur la gâchette (‹ › régler).","Mando derecho: apunta y pulsa el gatillo (‹ › ajustar).","Controller destro: punta e premi il grilletto (‹ › regola).","Prawy kontroler: wskaż i naciśnij spust (‹ › zmiana).","Controle direito: aponte e puxe o gatilho (‹ › ajustar)."},
        ["Left stick: select / adjust. A: confirm. B: close."]=new[]{"Левый стик: выбор / изменение. A: подтвердить. B: закрыть.","Linker Stick: wählen / ändern. A: bestätigen. B: schließen.","Stick gauche : choisir / régler. A : confirmer. B : fermer.","Stick izquierdo: elegir / ajustar. A: confirmar. B: cerrar.","Stick sinistro: scegli / regola. A: conferma. B: chiudi.","Lewy drążek: wybór / zmiana. A: zatwierdź. B: zamknij.","Analógico esquerdo: escolher / ajustar. A: confirmar. B: fechar."},
        ["Teleport: left stick forward, aim with the left hand, release. Back to cancel."]=new[]{"Телепорт: левый стик вперёд, навести левой рукой, отпустить стик. Стик назад — отмена.","Teleport: linken Stick vor, mit links zielen, loslassen. Zurück: abbrechen.","Téléportation : stick gauche vers l'avant, viser de la main gauche, relâcher. Arrière : annuler.","Teletransporte: stick izquierdo adelante, apunta con la izquierda y suelta. Atrás: cancelar.","Teletrasporto: stick sinistro avanti, mira con la sinistra e rilascia. Indietro: annulla.","Teleport: lewy drążek do przodu, celuj lewą ręką i puść. Do tyłu: anuluj.","Teletransporte: analógico esquerdo para frente, mire com a esquerda e solte. Para trás: cancelar."},
        ["Saved"]=new[]{"Сохранено","Gespeichert","Enregistré","Guardado","Salvato","Zapisano","Salvo"},
        ["Done"]=new[]{"Выполнено","Erledigt","Fait","Hecho","Fatto","Gotowe","Feito"},
        ["In 3 seconds: look ahead and hold your right hand forward"]=new[]{"Через 3 секунды: смотрите и держите правую руку вперёд","In 3 Sekunden: geradeaus schauen und die rechte Hand nach vorn halten","Dans 3 secondes : regardez devant et tendez la main droite","En 3 segundos: mira al frente y extiende la mano derecha","Tra 3 secondi: guarda avanti e tieni la mano destra in avanti","Za 3 sekundy: patrz przed siebie i trzymaj prawą rękę wyciągniętą","Em 3 segundos: olhe para frente e estenda a mão direita"},
        ["Applying..."]=new[]{"Применение...","Wird angewendet...","Application...","Aplicando...","Applicazione...","Stosowanie...","Aplicando..."},
        ["Waiting for the new size..."]=new[]{"Ожидание нового размера...","Warte auf die neue Größe...","En attente de la nouvelle taille...","Esperando el nuevo tamaño...","In attesa della nuova dimensione...","Oczekiwanie na nowy rozmiar...","Aguardando o novo tamanho..."},
        ["Already set"]=new[]{"Уже установлено","Bereits eingestellt","Déjà défini","Ya establecido","Già impostato","Już ustawiono","Já definido"},
        ["Applied:"]=new[]{"Применено:","Angewendet:","Appliqué :","Aplicado:","Applicato:","Zastosowano:","Aplicado:"},
        ["Could not apply"]=new[]{"Не удалось применить","Konnte nicht angewendet werden","Impossible d'appliquer","No se pudo aplicar","Impossibile applicare","Nie udało się zastosować","Não foi possível aplicar"},
        ["Saved. Size not confirmed — restart the game."]=new[]{"Сохранено. Размер не подтверждён — перезапустите игру.","Gespeichert. Größe nicht bestätigt — Spiel neu starten.","Enregistré. Taille non confirmée — redémarrez le jeu.","Guardado. Tamaño no confirmado: reinicia el juego.","Salvato. Dimensione non confermata — riavvia il gioco.","Zapisano. Rozmiar niepotwierdzony — uruchom grę ponownie.","Salvo. Tamanho não confirmado — reinicie o jogo."},
        ["Resolution not available yet"]=new[]{"Разрешение пока недоступно","Auflösung noch nicht verfügbar","Résolution pas encore disponible","Resolución aún no disponible","Risoluzione non ancora disponibile","Rozdzielczość jeszcze niedostępna","Resolução ainda indisponível"},
    };
    // Translation of an English source text; code null = the game's current language.
    internal static string L(string english,string? code=null)
    {
        code=(code??Code).Split('-','_')[0].ToLowerInvariant();
        int i=Array.IndexOf(Languages,code);
        if(i<0||!Table.TryGetValue(english,out var words))return english;
        return words[i];
    }
    internal static bool Has(string english)=>Table.ContainsKey(english);
    internal static string Watch(int slot)=>Code switch
    {
        "ru"=>new[]{"ПАТРОНЫ","ЖИЗНЬ","ЗАПАС","БРОНЯ"}[slot],
        "de"=>new[]{"MUNITION","LEBEN","VORRAT","PANZER"}[slot],
        "fr"=>new[]{"MUNITIONS","VIE","RESERVE","ARMURE"}[slot],
        "es"=>new[]{"MUNICION","SALUD","RESERVA","ARMADURA"}[slot],
        "it"=>new[]{"MUNIZIONI","SALUTE","RISERVA","ARMATURA"}[slot],
        "pl"=>new[]{"AMUNICJA","ZDROWIE","ZAPAS","PANCERZ"}[slot],
        "pt"=>new[]{"MUNICAO","VIDA","RESERVA","ARMADURA"}[slot],
        _=>new[]{"AMMO","HEALTH","RESERVE","ARMOR"}[slot]
    };
    internal static bool Manual {get=>ReadManual?.Invoke()??true;set=>WriteManual?.Invoke(value);}
}
