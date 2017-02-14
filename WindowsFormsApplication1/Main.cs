//#define DEBUG

using System;
using System.Windows.Forms;
using Awesomium.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using System.IO;
using System.Drawing;
using DPayed;

namespace debts {
    public partial class CheckDebts : Form {
        public enum State {
            Nothing,
            LoadingEmpty,
            LoadedEmpty,
            ResetSTS,
            LoadingTcard,
            LoadedTcard,
            Parsing
        }

        public const string DEFAULT_LOG_NAME = "DPayd.log";
        public const string SERVICE_NAME = "DPAYD";
        public const string MSG_WRONG = "Некорректные серия и номер СТС: ";
        public const string MSG_PROCESS_SUCCEFULL = "Процесс завершён успешно";
        public const string MSG_PROCESS_FAILED = "Процесс завершён с ошибкой: ";
        public const int MAX_REPEAT_LOAD_EMPTY = 30;
        public const string INN_PAYED = "PAYED";
        private const string UNDEFINED = "undefined";
        public const int DAYS_TO_PAY = 70;
        static public DateTime DEFAULT_DATE = new DateTime(1901, 1, 1);

        public class Status {
            public State state = State.Nothing;
            public DateTime timeURI = DEFAULT_DATE;
        }

        public CheckDebts() {
            InitializeComponent();
        }
        public dynamic summas;
        const string NULL_SPASE = "";
        const string SINGLE_SPACE = " ";
        const string DOUBLE_SPACE = "  ";
        const string CLOSING_TAG_P = "</p>";
        const int VREME_OGID_VVODA = 125;
        const string CLOSING_TAG_SPAN = "</span>";
        const int MAX_WAIT_CIRCLES = 30;
        public string hTML2;
        public string mesto;
        public string org_Vlasti;
        List<string> Listen = new List<string>();
        Debt debts = new Debt();
        public Status status = new Status();
        public int waitCirclesCount = 0;
        Gibdd_Reqs Gibdd_Reqs;
        // добавлено Nik
        DBProcessor dbProcessor = new DBProcessor();

        int repeatCntLoadEmpty = 0;

        string number_only(ref dynamic text) {
            // text = text.ToString();
            string resultat = "";
            for (int i = 0; i < text.Length; i++)
                if ((text[i] >= '0') && (text[i] <= '9'))
                    resultat += text[i];
            string tmp = resultat.Trim();
            //MessageBox.Show(tEXT);
            text = tmp;

            return text;
        }

        int getFirstNum(string text) {
            for (int i = 0; i < text.Length; i++)
                if ((text[i] >= '0') && (text[i] <= '9'))

                    return i;
            return -1;
        }

        async void waitHTMLAsync() {
            string locTcard = debts.Tcard;
#if DEBUG
            log("waitHTMLAsync() START Tcard=" + locTcard + ", state=" + status.state.ToString());
#endif
            await Task.Delay(1000); // 1 секундa
            process();
            /*
            switch (process()) {
                case State.LoadingTcard:
                    ...
                    break;
                case State.LoadedTcard:
                    break;
            }
            */
#if DEBUG
            log("waitHTMLAsync() END Tcard=" + locTcard + ", state=" + status.state.ToString());
#endif

        }

        public string replaceTab(string str) {
            return str.Replace("\t", " ");
        }

        public class Debt {
            //это поля из таблицы Debts, которые заполняются, поэтому я тебе буду давать всю эту структуру, вместо Tcard
            // а ты заполнишь те, которые отмечены * плюс те, которые хотел Барк в дополнение
            public SqlDecimal ID;
            public string Vclstamp;
            public SqlDecimal Vcl;
            public string Tcard;
            public string Reason; //+
            public string Ordinance; //+
            public DateTime Dbtdte = DEFAULT_DATE; //+
            public DateTime Ofndte = DEFAULT_DATE; // + 
            public SqlDecimal Sum; //+
            public SqlDecimal SumHalf; //+
            public DateTime Paytodte = DEFAULT_DATE; //+
            public DateTime PaytoHalf = DEFAULT_DATE; //+
            public SqlDecimal Brn;
            public string Brnname;
            public string Regnum;
            public string Place;
            public string inn;
        }

        public void log(string msg, string logName = DEFAULT_LOG_NAME) {
            listBox1.Items.Add(msg);
            listBox1.SelectedIndex = listBox1.Items.Count - 1;
            DateTime currtime = DateTime.Now;

            using (StreamWriter file = new StreamWriter(logName, true)) {
                file.WriteLine(currtime + "   " + msg);
                file.Close();
            }
        }

        public void logMsg(string msg, string logName = DEFAULT_LOG_NAME) {
            log(msg, logName);
            MessageBox.Show(msg);
        }

        public string getField(string body, string paramName, string endTag = CLOSING_TAG_P) {
            paramName = ">" + paramName + ":</span>";
            int paramLength = paramName.Length;
            int start = body.IndexOf(paramName) + paramLength;
            body = body.Substring(start);
            int end = body.IndexOf(endTag);
            return removeSpaceSequences(body.Remove(end)).Trim();
        }

        public string removeSpaceSequences(string str) {
            while (str.IndexOf(DOUBLE_SPACE) >= 0)
                str = str.Replace(DOUBLE_SPACE, SINGLE_SPACE);

            return str;
        }

        private void button1_Click(object sender, EventArgs e) {
            source_HTML();
        }

        private Debt nextTcard() {
            // Здесь надо подкл, к БД и получить debts.Tcard.
            Debt nextDebts = new Debt();

            // Передача параметра ПО ЗНАЧЕНИЮ!!!
            if (!dbProcessor.getNextTcard(ref nextDebts)) {
                if (dbProcessor.state == DBProcessor.State.Succefull) {
                    logMsg(MSG_PROCESS_SUCCEFULL);
                } else {
                    logMsg(MSG_PROCESS_FAILED + status);
                }
                Application.Exit();
            }

            status.state = State.LoadingTcard;

            return nextDebts;
        }

        private bool workDB(Debt rec) {
            //Здесь надо положить в ДБ debts.Pravonarushenie и т.д
            bool check = dbProcessor.check(ref rec);
            return check ? dbProcessor.update(rec) : dbProcessor.insert(rec);
        }

        bool isUndefined(string str) {
            return str == UNDEFINED;
        }

        private void process() {
            string locTcard = debts.Tcard;
#if DEBUG
            log("process() START Tcard=" + locTcard + ", state=" + status.state.ToString());
#endif
            dynamic document = (JSObject)webControlMosPgu.ExecuteJavascriptWithResult("document");

            string loaderBar = (document.getElementsByClassName("loader")[0]).getAttribute("style");
            loaderBar = loaderBar.Replace(SINGLE_SPACE, NULL_SPASE);

            dynamic checkNoFines = document.getElementById("hasNoFines");
            string checkNoFindDebts = checkNoFines.getAttribute("style");
            checkNoFindDebts = checkNoFindDebts.Replace(SINGLE_SPACE, NULL_SPASE);

            string firstPayed = document.getElementById("payed").getElementsByClassName("rendered_charge_container")[0];

            string firstUnpayedDebt = document.getElementById("npayed").getElementsByClassName("rendered_charge_container")[0];

            /*
            log("process() MODE 0 TCard=" + locTcard + ", state=" + status.state.ToString());
            log("process() MODE 0 TCard=" + locTcard + ", firstPayed=" + firstPayed);
            log("process() MODE 0 TCard=" + locTcard + ", firstUnpayedDebt=" + firstUnpayedDebt);
            log("process() MODE 0 TCard=" + locTcard + ", checkNoFindDebts=" + checkNoFindDebts + "/" + loaderBar);
            */

            bool isNotLoaded = (checkNoFindDebts == "display:none;" || loaderBar == "display:block;");
            if (isUndefined(firstPayed) && isUndefined(firstUnpayedDebt) && isNotLoaded) {
#if DEBUG
                log("process() MODE 1 TCard=" + locTcard + ", state=" + status.state.ToString());
#endif
                if (++waitCirclesCount > MAX_WAIT_CIRCLES) {
                    source_HTML();
#if DEBUG
                    log("process() END BY MAX_WAIT_CIRCLES, TCard=" + locTcard + ", state=" + status.state.ToString());
#endif
                    return;
                }
                status.state = State.LoadingTcard;
                waitHTMLAsync();
            } else {
                waitCirclesCount = 0;
                if (!isUndefined(firstUnpayedDebt)) {
#if DEBUG
                    log("process() MODE 2.1 TCard=" + locTcard + ", state=" + status.state.ToString());
#endif
                    dannie(document);


                } else {
#if DEBUG
                    log("process() MODE 2.2 TCard=" + locTcard + ", state=" + status.state.ToString());
#endif
                    //if ((oter == "undefined") || fresh != "display:none;")
                    string LogStr = debts.Tcard + ", " + debts.Vclstamp + ", Штрафов не найдено!";
                    log(LogStr);
                }
                // удалить предыдущий СТС
                document.getElementsByClassName("as-close")[0].focus();
                document.getElementsByClassName("as-close")[0].click();

                status.state = State.LoadedTcard;
                vvod_and_click();
            }
#if DEBUG
            log("process() END Tcard=" + locTcard + ", state=" + status.state.ToString());
#endif
        }

        private void Awesomium_Windows_Forms_WebControl_DocumentReady(object sender, DocumentReadyEventArgs e) {
            if (e.ReadyState == DocumentReadyState.Loaded) {
                string locTcard = debts.Tcard;
#if DEBUG
                log("DocumentReady() START Tcard=" + locTcard + ", state=" + status.state.ToString());
#endif
                status.state = State.LoadedEmpty;
                vvod_and_click();
#if DEBUG
                log("DocumentReady() END Tcard=" + locTcard + ", state=" + status.state.ToString());
#endif
            }
        }

        private void source_HTML() {
            string locTcard = debts.Tcard;
#if DEBUG
            log("source_HTML() START Tcard=" + locTcard + ", state=" + status.state.ToString());
#endif
            status.state = State.LoadingEmpty;
            webControlMosPgu.Source = new Uri("https://www.mos.ru/pgu/ru/application/gibdd/fines/?utm_source=mos&utm_medium=ek&utm_campaign=85532&utm_term=884533#step_1");
#if DEBUG
            log("source_HTML() END Tcard=" + locTcard + ", state=" + status.state.ToString());
#endif
        }

        /*
                    document.getElementsByClassName("as-close")[0].click();
                    vvod_and_click();
        */

        private void dannie(dynamic document) {
            string locTcard = debts.Tcard;
#if DEBUG
            log("dannie() START Tcard=" + locTcard + ", state=" + status.state.ToString());
#endif
            int nomer = -1;
            while (true) {
                //dynamic document = (JSObject)webControlMosPgu.ExecuteJavascriptWithResult("document");
                nomer++;
                string Chec = document.getElementById("npayed").getElementsByClassName("rendered_charge_container")[nomer];

                if (Chec == "undefined") {
                    break;
                }

                dynamic rendConteiner = document.getElementById("npayed").getElementsByClassName("rendered_charge_container")[nomer];
                debts.Ordinance = rendConteiner.getAttribute("id");
                Clipboard.SetText(debts.Ordinance);

                string HTML = document.getElementById("npayed").getElementsByClassName("rendered_charge_container")[nomer].outerHTML;
                // MessageBox.Show(HTML);
                // Clipboard.SetText(HTML);
                hTML2 = HTML;

                string Prav = replaceTab(getField(HTML, "Правонарушение").Replace("&nbsp;", " "));
                debts.Reason = Prav;

                //debts.Reason = replaseTab(
                string DataPastanov = debts.Reason.Substring(debts.Reason.IndexOf("от") + 2, 12);
                string dte = DataPastanov.Trim();
                DateTime dbtdte = DateTime.Parse(dte);
                debts.Dbtdte = dbtdte;

                DateTime Paytodte = DateTime.Parse(dte);
                debts.Paytodte = Paytodte.AddDays(DAYS_TO_PAY);

                //MessageBox.Show(DbtDte);

                string datas = getField(HTML, "Дата нарушения");
                DateTime data = DateTime.Parse(datas);
                debts.Ofndte = data;



                if (HTML.IndexOf("Место нарушения") != -1) {
                    debts.Place = replaceTab(getField(HTML, "Место нарушения").Replace("&nbsp;", " "));
                } else {
                    debts.Place = "";
                }


                /*
                                    org_Vlasti = getField(HTML, "Орган власти");
                                    org_Vlasti = org_Vlasti.Replace("&nbsp;","");
                                    org_Vlasti = org_Vlasti.Replace("<br>", "");       Это не надо
                                    org_Vlasti = org_Vlasti.Trim();
                                    debts.Org_vlasti = org_Vlasti;
                */


                string Summa = getField(HTML, "Штраф");

                try {
                     summas = Summa.Substring(getFirstNum(Summa), 10);
                } catch (ArgumentOutOfRangeException e) {
                    log("Не удалось достать сумму!!! Tcard = " + debts.Tcard + "   Ошибка: " + e.Message);
                    debts.Sum = 0;
                    debts.SumHalf = 0;
                }
                    number_only(ref summas);
                    decimal summa = Convert.ToDecimal(summas);
                    debts.Sum = summa;

                    if (hTML2.IndexOf("strike") != -1) {
                        Summa = Summa.Remove(0, Summa.IndexOf(CLOSING_TAG_SPAN) + 7);
                        summas = Summa.Substring(getFirstNum(Summa), 10);
                        number_only(ref summas);
                        decimal summaHalf = Convert.ToDecimal(summas);
                        debts.SumHalf = summaHalf;

                        dynamic paydtoHalf = Summa.Remove(0, Summa.IndexOf(CLOSING_TAG_SPAN) + 7);
                        paydtoHalf = Summa.Remove(0, Summa.IndexOf("доступна до:"));
                        paydtoHalf = paydtoHalf.Remove(0, getFirstNum(paydtoHalf));
                        paydtoHalf = paydtoHalf.Remove(paydtoHalf.IndexOf("<"), 7);
                        paydtoHalf = paydtoHalf.Trim();
                        DateTime payToHalf = DateTime.Parse(paydtoHalf);
                        debts.PaytoHalf = payToHalf;
                    }

                    if (hTML2.IndexOf("strike") == -1) {
                        // debts.PaytoHalf = debts.PaytoHalf.MinValue;
                        debts.SumHalf = 0;
                    }
          

                string LogString = debts.Ordinance + "    " + dte + "   " + debts.Vclstamp + "   Проверено";
                log(LogString);
                if (!workDB(debts)) {
                    MessageBox.Show("Случилась какая-то ошибка во время записи в Debts...");
                }
                string LogStrin = debts.Ordinance + "    " + dte + "   " + debts.Vclstamp + "   Записано!";
                log(LogStrin);
            }
#if DEBUG
            log("dannie() END Tcard=" + locTcard + ", state=" + status.state.ToString());
#endif
        }

        private void vvod_and_click() {
            string locTcard = "";
#if DEBUG
            log("vvod_and_click() START state=" + status.state.ToString());
#endif
            if (dbProcessor.state == DBProcessor.State.Normal) {
                //Ввожу данные
                debts = nextTcard();
                locTcard = debts.Tcard;
#if DEBUG
                log("vvod_and_click() Tcard=" + locTcard);
#endif
                string sts = "";
                dynamic document = (JSObject)webControlMosPgu.ExecuteJavascriptWithResult("document");
                dynamic inputs = document.getElementsByTagName("input");
                int lenInputs = (int)inputs.length;
                bool stsValueWasDefined = false;
                for (int i = 0; i < lenInputs; i++) {
                    dynamic inp = document.getElementsByTagName("input")[i];
                    String inpName = inp.getAttribute("name");
                    if (inpName.Contains("as_values_")) {
                        sts = debts.Tcard.Replace("№", "").Trim();
                        inp.focus();
                        inp.value = sts;
                        document.getElementById("button_next").focus();
                        stsValueWasDefined = true;
                        break;
                    }
                }

                if (stsValueWasDefined) {
                    repeatCntLoadEmpty = 0;
                    dynamic buttonNext = document.getElementById("button_next");
                    buttonNext.focus();
                    buttonNext.click();

                    dynamic checkErrorMessage = document.getElementsByClassName("error error-message")[0];
                    string chkMsg = checkErrorMessage;
                    if (chkMsg != "undefined") {
                        log(MSG_WRONG + sts);
                        vvod_and_click();
                    } else {
                        waitHTMLAsync(); // TODO
                    }
                } else {
                    if (++repeatCntLoadEmpty > MAX_REPEAT_LOAD_EMPTY) {
                        logMsg("Ошибка загрузки начальной страницы");
                        Application.Exit();
                    }
                    source_HTML(); // TODO
                }
            } else {
                MessageBox.Show("dbProcessor.state != DBProcessor.State.Normal!!!!!!!");
                Application.Exit();
            }

#if DEBUG
            log("vvod_and_click() END Tcard=" + locTcard + ", state=" + status.state.ToString());
#endif
        }


        private void button2_Click(object sender, EventArgs e) {
            Gibdd_Reqs = new Gibdd_Reqs();
            Gibdd_Reqs.initGibddDivisions();
            //Process.Start(@"C:\Users\Kostya\Desktop\БЕЗ GIT!!!!\DpayD\WindowsFormsApplication1\bin\Release\Bat.bat");
            /*
            dynamic document = (JSObject)webControl1.ExecuteJavascriptWithResult("document");
            dynamic Cheсс = document.getElementsByClassName("error error-message")[0];
            string oops = Cheсс;
            if (oops == "undefined") {
                dannie();
            }
            if (oops != "undefined") {
                vvod_and_click();
            }
            */
        }

        private void Form1_Load(object sender, EventArgs e) {
            source_HTML();

        }

        private void timer1_Tick(object sender, EventArgs e) {

        }

        private void Awesomium_Windows_Forms_WebControl_ShowCreatedWebView(object sender, ShowCreatedWebViewEventArgs e) {

        }

        private void button1_Click_1(object sender, EventArgs e) {

        }

        private void listBox1_DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index < 0) return;
            //if the item state is selected them change the back color 
            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                e = new DrawItemEventArgs(e.Graphics,
                                          e.Font,
                                          e.Bounds,
                                          e.Index,
                                          e.State ^ DrawItemState.Selected,
                                          e.ForeColor,
                                          Color.YellowGreen);//Choose the color

            // Draw the background of the ListBox control for each item.
            e.DrawBackground();
            // Draw the current item text
            e.Graphics.DrawString(listBox1.Items[e.Index].ToString(), e.Font, Brushes.Black, e.Bounds, StringFormat.GenericDefault);
            // If the ListBox has focus, draw a focus rectangle around the selected item.
            e.DrawFocusRectangle();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e) {

        }

        private void CheckDebts_FormClosed(object sender, FormClosedEventArgs e) {
            WebCore.Shutdown();
        }
    }
}
