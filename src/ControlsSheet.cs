using System;
using System.Collections.Generic;
using System.Text;
namespace XiiiXR;
// 0.1.215: the VR controls page ("VR CONTROLS" in the pause menu): each
// action and the buttons or gesture that do it, for the dominant hand set in
// the VR settings, in the game's language.
internal static class ControlsSheet
{
    internal readonly record struct Row(string Action,string Right,string Left);
    internal static readonly Row[] Rows=
    {
        new("Move","Left stick (where you look)","Left stick (where you look)"),
        new("Turn","Right stick left / right","Right stick left / right"),
        new("Jump / crouch","Right stick up / down, or crouch for real","Right stick up / down, or crouch for real"),
        new("Take a weapon or a thing","Left/Right Grip at it","Left/Right Grip at it"),
        new("Hang a weapon back","Let go of the Grip at its place on the body","Let go of the Grip at its place on the body"),
        new("Fire","Trigger of the hand that holds the gun","Trigger of the hand that holds the gun"),
        new("Reload","Right B (manual reload: by hand)","Left Y (manual reload: by hand)"),
        new("Magazine, rounds (by hand)","Left Grip at the belt pouch, then into the gun","Right Grip at the belt pouch, then into the gun"),
        new("Bolt, pump (by hand)","Left Grip at the bolt or pump, pull back","Right Grip at the bolt or pump, pull back"),
        new("Machine gun cover","Right B opens it; push it shut by hand","Left Y opens it; push it shut by hand"),
        new("Bazooka rocket","The other Grip at the belt pouch, then into its front","The other Grip at the belt pouch, then into its front"),
        new("Two hands on a long gun","The other Grip on its barrel","The other Grip on its barrel"),
        new("Throw (knife, grenade, bottle)","Hold the Grip, swing, let go","Hold the Grip, swing, let go"),
        new("Grenade pin","The other hand's trigger at the grenade","The other hand's trigger at the grenade"),
        new("Punch, hit with a weapon","Grip + swing","Grip + swing"),
        new("Knock out from behind","Hard punch of either fist in his back","Hard punch of either fist in his back"),
        new("Door, grate, glass","By hand: push, pull, hit (or Right Grip + A)","By hand: push, pull, hit (or Left Grip + X)"),
        new("Key, card, lockpick","Right stick click (R3), then use it by hand","Left stick click (L3), then use it by hand"),
        new("Pick a lock","Turn the pick in the lock, hold it until the timer ends","Turn the pick in the lock, hold it until the timer ends"),
        new("Take a hostage","Point a free hand at him from behind, hold its Grip","Point a free hand at him from behind, hold its Grip"),
        new("Carry a body","Left Grip on it","Left Grip on it"),
        new("Weapon wheel","Hold right A, choose with the left stick","Hold right A, choose with the left stick"),
        new("Next weapon","Left Y","Left Y"),
        new("Tasks","Left X","Left X"),
        new("Medkit","On the forearm: the other hand's Grip takes it","On the forearm: the other hand's Grip takes it"),
        new("Pause menu","Left Grip + X","Right Grip + A"),
        new("Menu pointer","The hand whose trigger you pulled last","The hand whose trigger you pulled last"),
        new("Rope","Left stick climbs, right stick swings, L3 lets go","Left stick climbs, right stick swings, L3 lets go"),
        new("Skip a cutscene","Right trigger","Right trigger"),
        new("Recenter the view","F11 on the keyboard","F11 on the keyboard"),
    };
    private static readonly string[] Languages={"ru","de","fr","es","it","pl","pt"};
    private static readonly Dictionary<string,string[]> Table=new(StringComparer.Ordinal)
    {
        ["VR CONTROLS"]=new[]{"УПРАВЛЕНИЕ VR","VR-STEUERUNG","COMMANDES VR","CONTROLES VR","COMANDI VR","STEROWANIE VR","CONTROLES VR"},
        ["B: back"]=new[]{"B: назад","B: zurück","B : retour","B: volver","B: indietro","B: wstecz","B: voltar"},
        ["Move"]=new[]{"Идти","Gehen","Se déplacer","Moverse","Muoversi","Ruch","Mover-se"},
        ["Left stick (where you look)"]=new[]{"Левый стик (куда смотришь)","Linker Stick (in Blickrichtung)","Stick gauche (vers le regard)","Stick izquierdo (hacia donde miras)","Stick sinistro (dove guardi)","Lewy drążek (tam, gdzie patrzysz)","Analógico esquerdo (para onde olha)"},
        ["Turn"]=new[]{"Поворот","Drehen","Tourner","Girar","Girarsi","Obrót","Virar"},
        ["Right stick left / right"]=new[]{"Правый стик влево / вправо","Rechter Stick links / rechts","Stick droit gauche / droite","Stick derecho izquierda / derecha","Stick destro sinistra / destra","Prawy drążek w lewo / w prawo","Analógico direito esquerda / direita"},
        ["Jump / crouch"]=new[]{"Прыжок / присесть","Springen / ducken","Sauter / s'accroupir","Saltar / agacharse","Saltare / accovacciarsi","Skok / kucnięcie","Pular / agachar"},
        ["Right stick up / down, or crouch for real"]=new[]{"Правый стик вверх / вниз или присесть по-настоящему","Rechter Stick hoch / runter oder wirklich in die Hocke","Stick droit haut / bas, ou accroupissez-vous vraiment","Stick derecho arriba / abajo, o agáchate de verdad","Stick destro su / giù, o accovacciati davvero","Prawy drążek w górę / w dół albo kucnij naprawdę","Analógico direito cima / baixo, ou agache de verdade"},
        ["Take a weapon or a thing"]=new[]{"Взять оружие или предмет","Waffe oder Gegenstand nehmen","Prendre une arme ou un objet","Coger un arma o un objeto","Prendere un'arma o un oggetto","Weź broń lub przedmiot","Pegar uma arma ou um objeto"},
        ["Left/Right Grip at it"]=new[]{"Левый/правый Grip у предмета","Linker/rechter Grip daran","Grip gauche/droit dessus","Grip izquierdo/derecho sobre él","Grip sinistro/destro sull'oggetto","Lewy/prawy Grip przy nim","Grip esquerdo/direito nele"},
        ["Hang a weapon back"]=new[]{"Повесить оружие обратно","Waffe zurückhängen","Ranger une arme","Guardar un arma","Riporre un'arma","Odwieś broń","Guardar uma arma"},
        ["Let go of the Grip at its place on the body"]=new[]{"Отпустить Grip у его места на теле","Grip an ihrem Platz am Körper loslassen","Relâcher le Grip à sa place sur le corps","Suelta el Grip en su sitio del cuerpo","Rilascia il Grip al suo posto sul corpo","Puść Grip przy jej miejscu na ciele","Solte o Grip no lugar dela no corpo"},
        ["Fire"]=new[]{"Стрелять","Schießen","Tirer","Disparar","Sparare","Strzał","Atirar"},
        ["Trigger of the hand that holds the gun"]=new[]{"Триггер руки с оружием","Abzug der Hand mit der Waffe","Gâchette de la main qui tient l'arme","Gatillo de la mano que sostiene el arma","Grilletto della mano che tiene l'arma","Spust ręki trzymającej broń","Gatilho da mão que segura a arma"},
        ["Reload"]=new[]{"Перезарядка","Nachladen","Recharger","Recargar","Ricaricare","Przeładowanie","Recarregar"},
        ["Right B (manual reload: by hand)"]=new[]{"Правая B (ручная перезарядка: руками)","Rechts B (manuelles Nachladen: von Hand)","B droit (rechargement manuel : à la main)","B derecho (recarga manual: a mano)","B destro (ricarica manuale: a mano)","Prawy B (ręczne przeładowanie: rękami)","B direito (recarga manual: à mão)"},
        ["Left Y (manual reload: by hand)"]=new[]{"Левая Y (ручная перезарядка: руками)","Links Y (manuelles Nachladen: von Hand)","Y gauche (rechargement manuel : à la main)","Y izquierdo (recarga manual: a mano)","Y sinistro (ricarica manuale: a mano)","Lewy Y (ręczne przeładowanie: rękami)","Y esquerdo (recarga manual: à mão)"},
        ["Bazooka rocket"]=new[]{"Ракета базуки","Bazooka-Rakete","Roquette du bazooka","Cohete del bazuca","Razzo del bazooka","Rakieta bazooki","Foguete da bazuca"},
        ["The other Grip at the belt pouch, then into its front"]=new[]{"Грип другой руки у подсумка на поясе, затем в базуку спереди","Anderer Griff an der Gürteltasche, dann vorne hinein","Grip de l'autre main à la sacoche, puis dans l'avant","Grip de la otra mano en la bolsa del cinturón, luego por delante","Grip dell'altra mano alla tasca, poi davanti nel tubo","Grip drugiej ręki przy ładownicy, potem od przodu","Grip da outra mão na bolsa do cinto, depois pela frente"},
        ["Left Grip at the bolt or pump, pull back"]=new[]{"Левый грип у затвора или помпы, оттянуть","Linker Griff am Verschluss oder an der Pumpe, zurückziehen","Grip gauche à la culasse ou à la pompe, tirer","Grip izquierdo en el cerrojo o la bomba, tirar","Grip sinistro all'otturatore o alla pompa, tirare","Lewy grip przy zamku lub pompce, odciągnij","Grip esquerdo no ferrolho ou na bomba, puxe"},
        ["Right Grip at the bolt or pump, pull back"]=new[]{"Правый грип у затвора или помпы, оттянуть","Rechter Griff am Verschluss oder an der Pumpe, zurückziehen","Grip droit à la culasse ou à la pompe, tirer","Grip derecho en el cerrojo o la bomba, tirar","Grip destro all'otturatore o alla pompa, tirare","Prawy grip przy zamku lub pompce, odciągnij","Grip direito no ferrolho ou na bomba, puxe"},
        ["Machine gun cover"]=new[]{"Крышка пулемёта","MG-Deckel","Couvercle de la mitrailleuse","Tapa de la ametralladora","Coperchio della mitragliatrice","Pokrywa karabinu maszynowego","Tampa da metralhadora"},
        ["Right B opens it; push it shut by hand"]=new[]{"Правая B открывает; закройте, надавив рукой","Rechts B öffnet; mit der Hand zudrücken","B droit l'ouvre ; refermez-le en appuyant à la main","B derecho la abre; ciérrala empujando con la mano","B destro lo apre; chiudilo premendo con la mano","Prawy B otwiera; zamknij, dociskając ręką","B direito abre; feche empurrando com a mão"},
        ["Left Y opens it; push it shut by hand"]=new[]{"Левая Y открывает; закройте, надавив рукой","Links Y öffnet; mit der Hand zudrücken","Y gauche l'ouvre ; refermez-le en appuyant à la main","Y izquierdo la abre; ciérrala empujando con la mano","Y sinistro lo apre; chiudilo premendo con la mano","Lewy Y otwiera; zamknij, dociskając ręką","Y esquerdo abre; feche empurrando com a mão"},
        ["Bolt, pump (by hand)"]=new[]{"Затвор, помпа (руками)","Verschluss, Pumpe (per Hand)","Culasse, pompe (à la main)","Cerrojo, bomba (a mano)","Otturatore, pompa (a mano)","Zamek, pompka (ręcznie)","Ferrolho, bomba (à mão)"},
        ["Magazine, rounds (by hand)"]=new[]{"Магазин, патроны (руками)","Magazin, Patronen (per Hand)","Chargeur, munitions (à la main)","Cargador, balas (a mano)","Caricatore, colpi (a mano)","Magazynek, naboje (ręcznie)","Carregador, balas (à mão)"},
        ["Left Grip at the belt pouch, then into the gun"]=new[]{"Левый грип у подсумка на поясе, затем в оружие","Linker Griff an der Gürteltasche, dann in die Waffe","Grip gauche à la sacoche de ceinture, puis dans l'arme","Grip izquierdo en la bolsa del cinturón, luego al arma","Grip sinistro alla tasca della cintura, poi nell'arma","Lewy grip przy ładownicy, potem do broni","Grip esquerdo na bolsa do cinto, depois na arma"},
        ["Right Grip at the belt pouch, then into the gun"]=new[]{"Правый грип у подсумка на поясе, затем в оружие","Rechter Griff an der Gürteltasche, dann in die Waffe","Grip droit à la sacoche de ceinture, puis dans l'arme","Grip derecho en la bolsa del cinturón, luego al arma","Grip destro alla tasca della cintura, poi nell'arma","Prawy grip przy ładownicy, potem do broni","Grip direito na bolsa do cinto, depois na arma"},
        ["Two hands on a long gun"]=new[]{"Длинное оружие двумя руками","Langwaffe beidhändig","Arme longue à deux mains","Arma larga a dos manos","Arma lunga a due mani","Długa broń oburącz","Arma longa com as duas mãos"},
        ["The other Grip on its barrel"]=new[]{"Grip другой руки на стволе","Der andere Grip am Lauf","L'autre Grip sur le canon","El otro Grip en el cañón","L'altro Grip sulla canna","Drugi Grip na lufie","O outro Grip no cano"},
        ["Throw (knife, grenade, bottle)"]=new[]{"Бросок (нож, граната, бутылка)","Werfen (Messer, Granate, Flasche)","Lancer (couteau, grenade, bouteille)","Lanzar (cuchillo, granada, botella)","Lanciare (coltello, granata, bottiglia)","Rzut (nóż, granat, butelka)","Arremessar (faca, granada, garrafa)"},
        ["Hold the Grip, swing, let go"]=new[]{"Держать Grip, замах, отпустить","Grip halten, ausholen, loslassen","Tenir le Grip, élan, relâcher","Mantén el Grip, balancea, suelta","Tieni il Grip, slancio, rilascia","Trzymaj Grip, zamach, puść","Segure o Grip, balance, solte"},
        ["Grenade pin"]=new[]{"Чека гранаты","Granatensplint","Goupille de grenade","Anilla de granada","Spoletta della granata","Zawleczka granatu","Pino da granada"},
        ["The other hand's trigger at the grenade"]=new[]{"Триггер другой руки у гранаты","Abzug der anderen Hand an der Granate","Gâchette de l'autre main sur la grenade","Gatillo de la otra mano en la granada","Grilletto dell'altra mano sulla granata","Spust drugiej ręki przy granacie","Gatilho da outra mão na granada"},
        ["Punch, hit with a weapon"]=new[]{"Удар рукой или оружием","Schlagen, mit der Waffe zuschlagen","Frapper (poing ou arme)","Golpear (puño o arma)","Colpire (pugno o arma)","Cios pięścią lub bronią","Golpear (soco ou arma)"},
        ["Knock out from behind"]=new[]{"Вырубить со спины","Von hinten ausknocken","Assommer par derrière","Noquear por la espalda","Stendere alle spalle","Ogłusz od tyłu","Nocautear por trás"},
        ["Hard punch of either fist in his back"]=new[]{"Сильный удар кулаком любой руки в спину","Kräftiger Faustschlag einer Hand in den Rücken","Coup de poing appuyé, d'une main, dans le dos","Puñetazo fuerte con cualquier mano en la espalda","Pugno forte di una mano qualsiasi nella schiena","Mocny cios pięścią dowolnej ręki w plecy","Soco forte de qualquer mão nas costas"},
        ["Door, grate, glass"]=new[]{"Дверь, решётка, стекло","Tür, Gitter, Glas","Porte, grille, vitre","Puerta, reja, cristal","Porta, grata, vetro","Drzwi, krata, szyba","Porta, grade, vidro"},
        ["By hand: push, pull, hit (or Right Grip + A)"]=new[]{"Рукой: толкнуть, потянуть, ударить (или правый Grip + A)","Von Hand: drücken, ziehen, schlagen (oder rechter Grip + A)","À la main : pousser, tirer, frapper (ou Grip droit + A)","A mano: empujar, tirar, golpear (o Grip derecho + A)","A mano: spingere, tirare, colpire (o Grip destro + A)","Ręką: pchnij, pociągnij, uderz (albo prawy Grip + A)","À mão: empurrar, puxar, bater (ou Grip direito + A)"},
        ["By hand: push, pull, hit (or Left Grip + X)"]=new[]{"Рукой: толкнуть, потянуть, ударить (или левый Grip + X)","Von Hand: drücken, ziehen, schlagen (oder linker Grip + X)","À la main : pousser, tirer, frapper (ou Grip gauche + X)","A mano: empujar, tirar, golpear (o Grip izquierdo + X)","A mano: spingere, tirare, colpire (o Grip sinistro + X)","Ręką: pchnij, pociągnij, uderz (albo lewy Grip + X)","À mão: empurrar, puxar, bater (ou Grip esquerdo + X)"},
        ["Pick a lock"]=new[]{"Вскрыть замок","Schloss knacken","Crocheter une serrure","Forzar una cerradura","Scassinare una serratura","Otworzyć zamek wytrychem","Arrombar uma fechadura"},
        ["Turn the pick in the lock, hold it until the timer ends"]=new[]{"Поверните отмычку в замке и держите до конца таймера","Dietrich im Schloss drehen, halten bis der Timer abläuft","Tournez le crochet, gardez-le jusqu'à la fin du minuteur","Gira la ganzúa y mantenla hasta que acabe el tiempo","Gira il grimaldello e tienilo finché finisce il timer","Przekręć wytrych w zamku i trzymaj do końca odliczania","Gire a gazua na fechadura e segure até o tempo acabar"},
        ["Key, card, lockpick"]=new[]{"Ключ, карта, отмычка","Schlüssel, Karte, Dietrich","Clé, carte, crochet","Llave, tarjeta, ganzúa","Chiave, tessera, grimaldello","Klucz, karta, wytrych","Chave, cartão, gazua"},
        ["Right stick click (R3), then use it by hand"]=new[]{"Нажать правый стик (R3), дальше рукой","Rechten Stick drücken (R3), dann von Hand","Clic du stick droit (R3), puis à la main","Pulsar stick derecho (R3), luego a mano","Premi lo stick destro (R3), poi a mano","Wciśnij prawy drążek (R3), dalej ręką","Pressione o analógico direito (R3), depois à mão"},
        ["Left stick click (L3), then use it by hand"]=new[]{"Нажать левый стик (L3), дальше рукой","Linken Stick drücken (L3), dann von Hand","Clic du stick gauche (L3), puis à la main","Pulsar stick izquierdo (L3), luego a mano","Premi lo stick sinistro (L3), poi a mano","Wciśnij lewy drążek (L3), dalej ręką","Pressione o analógico esquerdo (L3), depois à mão"},
        ["Take a hostage"]=new[]{"Взять заложника","Geisel nehmen","Prendre un otage","Tomar un rehén","Prendere un ostaggio","Weź zakładnika","Fazer um refém"},
        ["Point a free hand at him from behind, hold its Grip"]=new[]{"Навести свободную руку со спины, держать её Grip","Freie Hand von hinten auf ihn richten, ihren Grip halten","Pointez une main libre sur lui par derrière, tenez son Grip","Apunta una mano libre por detrás, mantén su Grip","Punta una mano libera su di lui da dietro, tieni il suo Grip","Wyceluj wolną ręką w niego od tyłu, trzymaj jej Grip","Aponte uma mão livre para ele por trás, segure o Grip dela"},
        ["Carry a body"]=new[]{"Нести тело","Leiche tragen","Porter un corps","Cargar un cuerpo","Trasportare un corpo","Nieś ciało","Carregar um corpo"},
        ["Left Grip on it"]=new[]{"Левый Grip на теле","Linker Grip daran","Grip gauche dessus","Grip izquierdo sobre él","Grip sinistro sul corpo","Lewy Grip na ciele","Grip esquerdo nele"},
        ["Weapon wheel"]=new[]{"Колесо оружия","Waffenrad","Roue des armes","Rueda de armas","Ruota delle armi","Koło broni","Roda de armas"},
        ["Hold right A, choose with the left stick"]=new[]{"Держать правую A, выбор левым стиком","Rechts A halten, mit dem linken Stick wählen","Maintenir A droit, choisir au stick gauche","Mantén A derecho, elige con el stick izquierdo","Tieni premuto A destro, scegli con lo stick sinistro","Trzymaj prawy A, wybierz lewym drążkiem","Segure A direito, escolha com o analógico esquerdo"},
        ["Next weapon"]=new[]{"Следующее оружие","Nächste Waffe","Arme suivante","Arma siguiente","Arma successiva","Następna broń","Próxima arma"},
        ["Tasks"]=new[]{"Задания","Aufgaben","Objectifs","Objetivos","Obiettivi","Zadania","Objetivos"},
        ["Medkit"]=new[]{"Аптечка","Medikit","Trousse de soins","Botiquín","Medikit","Apteczka","Kit médico"},
        ["On the forearm: the other hand's Grip takes it"]=new[]{"На предплечье: Grip другой руки берёт её","Am Unterarm: der Grip der anderen Hand nimmt es","Sur l'avant-bras : le Grip de l'autre main la prend","En el antebrazo: el Grip de la otra mano lo coge","Sull'avambraccio: il Grip dell'altra mano lo prende","Na przedramieniu: bierze ją Grip drugiej ręki","No antebraço: o Grip da outra mão o pega"},
        ["Pause menu"]=new[]{"Меню паузы","Pausenmenü","Menu pause","Menú de pausa","Menu di pausa","Menu pauzy","Menu de pausa"},
        ["Menu pointer"]=new[]{"Указка в меню","Menüzeiger","Pointeur de menu","Puntero del menú","Puntatore del menu","Wskaźnik menu","Ponteiro do menu"},
        ["The hand whose trigger you pulled last"]=new[]{"Рука, чей триггер нажат последним","Die Hand, deren Abzug zuletzt gedrückt wurde","La main dont la gâchette a été pressée en dernier","La mano cuyo gatillo pulsaste último","La mano il cui grilletto hai premuto per ultimo","Ręka, której spust wciśnięto ostatnio","A mão cujo gatilho foi pressionado por último"},
        ["Rope"]=new[]{"Канат","Seil","Corde","Cuerda","Corda","Lina","Corda"},
        ["Left stick climbs, right stick swings, L3 lets go"]=new[]{"Левый стик — лезть, правый — раскачка, L3 — отцепиться","Linker Stick klettert, rechter schwingt, L3 lässt los","Stick gauche : grimper, stick droit : se balancer, L3 : lâcher","Stick izquierdo: trepar, stick derecho: balancearse, L3: soltarse","Stick sinistro: arrampicarsi, stick destro: dondolare, L3: mollare","Lewy drążek: wspinaczka, prawy: bujanie, L3: puszczenie","Analógico esquerdo: subir, direito: balançar, L3: soltar"},
        ["Skip a cutscene"]=new[]{"Пропустить ролик","Zwischensequenz überspringen","Passer une cinématique","Saltar una cinemática","Saltare un filmato","Pomiń przerywnik","Pular uma cena"},
        ["Recenter the view"]=new[]{"Выровнять вид","Ansicht zentrieren","Recentrer la vue","Recentrar la vista","Ricentrare la vista","Wyśrodkuj widok","Recentralizar a visão"},
        ["F11 on the keyboard"]=new[]{"F11 на клавиатуре","F11 auf der Tastatur","F11 sur le clavier","F11 en el teclado","F11 sulla tastiera","F11 na klawiaturze","F11 no teclado"},
    };
    // A text of this page in the language (code null: the game's); the
    // mod's other texts come from UiLanguage.
    internal static string T(string english,string? code=null)
    {
        string c=(code??UiLanguage.Code).Split('-','_')[0].ToLowerInvariant();
        int i=Array.IndexOf(Languages,c);
        if(i>=0&&Table.TryGetValue(english,out var words))return words[i];
        return UiLanguage.L(english,c);
    }
    internal static bool Translated(string english,string code)=>T(english,code)!=english;
    internal static IEnumerable<string> Texts()
    {
        yield return "VR CONTROLS";yield return "B: back";
        foreach(var r in Rows){yield return r.Action;yield return r.Right;yield return r.Left;}
    }
    // The page body: one line per action, the buttons in a second column.
    internal static string Body(bool leftHanded,string? code=null)
    {
        var text=new StringBuilder();
        foreach(var r in Rows)
            text.Append("<color=#9FDFFF>").Append(T(r.Action,code)).Append("</color><pos=40%>").Append(T(leftHanded?r.Left:r.Right,code)).Append('\n');
        return text.ToString().TrimEnd('\n');
    }
    internal static string Title(bool leftHanded,string? code=null)=>T("VR CONTROLS",code)+"  <size=70%>("+UiLanguage.L(leftHanded?"left-handed":"right-handed",code)+")</size>";
    // The page opened from the pause menu.
    internal static bool Open{get;private set;}
    internal static void Show()=>Open=true;
    internal static void Close()=>Open=false;
}
