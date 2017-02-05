using System;
using System.Windows.Forms;
using Awesomium.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlTypes;

namespace debts {
    public partial class CheckDebts : Form {
        public const string DEFAULT_LOG_NAME = "DPayd.log";
        public const string SERVICE_NAME = "DPAYD";
        public const string MSG_WRONG = "Некорректные серия и номер";
        public const string INN_PAYED = "PAYED";
        public const int DAYS_TO_PAY = 70;
        static public DateTime DEFAULT_DATE = new DateTime(1901, 1, 1);

        public CheckDebts() {
            InitializeComponent();

        }
        const string SINGLE_SPACE = " ";
        const string DOUBLE_SPACE = "  ";
        const string CLOSING_TAG_P = "</p>";
        const int VREME_OGID_VVODA = 125;
        const string CLOSING_TAG_SPAN = "</span>";
        public string hTML2;
        public string mesto;
        public string org_Vlasti;
        public string oops;
        public int nomer = -1;
        List<string> Listen = new List<string>();
        Debt debts = new Debt();
        DateTime lastGetUriTime = DateTime.Now;
        DateTime Checc;

        // добавлено Nik
        DBProcessor dbProcessor = new DBProcessor();

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

        async void But() {
            await Task.Delay(3000); // 1 секундa
            process();
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
            source_HTTP();
        }

        private Debt Tcarting() {
            // Здесь надо подкл, к БД и получить debts.Tcard.
            Debt nextDebts = new Debt();
            // Передача параметра ПО ЗНАЧЕНИЮ!!!

            if (!dbProcessor.getNextTcard(ref nextDebts)) {
                if (dbProcessor.state == DBProcessor.State.Normal)
                    MessageBox.Show("Процесс завершён успешно");
                else
                    MessageBox.Show("При попытке чтения произошла критическая ошибка: " + dbProcessor.state);

                Application.Exit();
            }

            return nextDebts;
        }
        //        dynamic document = (JSObject)webControl1.ExecuteJavascriptWithResult("document");
        //        dynamic inputs = document.getElementsByTagName("input");
        //        int lenInputs = (int)inputs.length;
        //            for (int i = 0; i<lenInputs; i++) {
        //                dynamic inp = document.getElementsByTagName("input")[i];
        //        String inpName = inp.getAttribute("name");
        //                if (inpName.Contains("as_values_")) {
        //                    debts.Tcard = Tcarting().Tcard;
        //            if (debts.Tcard != "") {
        //                MessageBox.Show(debts.Tcard);
        //                vvod_and_click(); // !!!!!!!!!!!!! это неправильно: вызывать функцию из себя же (специальный приём - рекурсия).
        //                                  // Но он здесь ни к чему. Кроме того, при рекурсии параметры передаются через стек, который может переполниться (он ограничен)
        //                                  // !!!! Используй цикл, например while (Ты понимаешь, что такое функция? Что такое вернуть значение?)
        //        debts.Tcard = "";
        //            }

        //            if (debts.Tcard == "") {
        //                MessageBox.Show("Tcard закончИлись");
        //            }

        //inp.focus();
        //                inp.value = "77УЕ093445";
        //                int strLen = debts.Tcard.Length;
        //                if (strLen == 10) {
        //                    inp.value = debts.Tcard;
        //                }

        //                if (strLen > 10) {
        //                    if (debts.Tcard.IndexOf('№') > -1) {
        //                        debts.Tcard = debts.Tcard.Replace("№", "");
        //                        inp.value = debts.Tcard;
        //                    }
        //                }

        //                //  MessageBox.Show(inp.getAttribute("id"));
        //                break;
        //            }
        //            // debts.Tcard = null;
        //        }
        //        dynamic b = document.getElementById("button_next").focus();
        //dynamic buttonNext = document.getElementById("button_next").click();
        ////buttonNext.focus();
        ////buttonNext.click();

        //dynamic Che = document.getElementsByClassName("error error-message")[0];
        //string oops = Che;
        //            if (oops != "undefined") {
        //                vvod_and_click();
        //    }
        //            But();
        //}

        private bool workDB(Debt rec) {
            //Здесь надо положить в ДБ debts.Pravonarushenie и т.д
            bool check = dbProcessor.check(ref rec);
            return check ? dbProcessor.update(rec) : dbProcessor.insert(rec);
        }

        private void process() {
            dynamic document = (JSObject)webControl1.ExecuteJavascriptWithResult("document");
            string Checpay = document.getElementById("payed").getElementsByClassName("rendered_charge_container")[0];
            dynamic Chect = document.getElementById("npayed").getElementsByClassName("rendered_charge_container")[0];
            dynamic Chec1 = document.getElementById("hasNoFines");
            string fresh = Chec1.getAttribute("style");

            string oter = Chect;

            if(Checpay != "undefined" && oter == "undefined") {
                nomer = -1;
                source_HTTP();
                nomer = -1;
            }

            if (fresh == "display:none;" && Checpay != "undefined" && oter == "undefined") {
                nomer = -1;
                source_HTTP();
                nomer = -1;
            }

            if (fresh != "display:none;") {
                nomer = -1;
                source_HTTP();
                nomer = -1;
            }

            if (oter != "undefined") {
                nomer = -1;
                dannie();
                nomer = -1;
            }


            if (Checpay == "undefined" && oter == "undefined" && fresh == "display:none;") {
                But();
            }
        }

        private void Awesomium_Windows_Forms_WebControl_DocumentReady(object sender, DocumentReadyEventArgs e) {
            //MessageBox.Show("Вызов");
            if (e.ReadyState == DocumentReadyState.Loaded)
                vvod_and_click();
        }

        private void source_HTTP() {
            webControl1.Source = new Uri("https://www.mos.ru/pgu/ru/application/gibdd/fines/?utm_source=mos&utm_medium=ek&utm_campaign=85532&utm_term=884533#step_1");
        }

        private void dannie() {
            while (nomer != -10) {
                if (nomer == -10) 
                    break;
                
                dynamic document = (JSObject)webControl1.ExecuteJavascriptWithResult("document");
                nomer++;
                string Chec = document.getElementById("npayed").getElementsByClassName("rendered_charge_container")[nomer];

                if (Chec == "undefined" && nomer == 0) {
                    MessageBox.Show("Штрафов не нашлось");
                    source_HTTP();
                    nomer = -10;
                    break;
                }

                if (Chec == "undefined") {
                    nomer = -10;
                    source_HTTP();
                    break;
                }


                if (Chec != "undefined") {
                    dynamic rendConteiner = document.getElementById("npayed").getElementsByClassName("rendered_charge_container")[nomer];
                    debts.Ordinance = rendConteiner.getAttribute("id");
                    dynamic HTML = document.getElementById("npayed").getElementsByClassName("rendered_charge_container")[nomer].outerHTML;
                    // MessageBox.Show(HTML);
                    //   Clipboard.SetText(HTML);
                    hTML2 = HTML;
                    debts.Reason = getField(HTML, "Правонарушение");

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
                    /*
                                        mesto = getField(HTML, "Место нарушения");      Это не надо
                                        debts.Mesto = mesto;

                                        org_Vlasti = getField(HTML, "Орган власти");
                                        org_Vlasti = org_Vlasti.Replace("&nbsp;","");
                                        org_Vlasti = org_Vlasti.Replace("<br>", "");       Это не надо
                                        org_Vlasti = org_Vlasti.Trim();
                                        debts.Org_vlasti = org_Vlasti;
                    */

                    string Summa = getField(HTML, "Штраф");
                    dynamic summas = Summa.Substring(getFirstNum(Summa), 10);
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
                    Clipboard.SetText("Преверено!!!!!!!!!!!!!");
                    if (!workDB(debts)) {
                        MessageBox.Show("Случилась какая-то ошибка во время записи в Debts...");
                    }
                }
            }
        }

        private void vvod_and_click() {
            debts = Tcarting();
            string sts = debts.Tcard;
            if (dbProcessor.state == DBProcessor.State.Normal) {
                //Ввожу данные
                dynamic document = (JSObject)webControl1.ExecuteJavascriptWithResult("document");
                dynamic inputs = document.getElementsByTagName("input");
                int lenInputs = (int)inputs.length;
                for (int i = 0; i < lenInputs; i++) {
                    dynamic inp = document.getElementsByTagName("input")[i];
                    String inpName = inp.getAttribute("name");
                    if (inpName.Contains("as_values_")) {
                        //debts.Tcard = "77УЕ093445";
                        // inp.value = debts.Tcard;
                        int strLen = sts.Length;
                        if (strLen == 10) {
                            inp.focus();
                            inp.value = sts;
                            dynamic hdshd = document.getElementById("driverLicenceSerie").focus();
                            System.Threading.Thread.Sleep(1000);
                            debts.Tcard = "";
                            break;
                            //  inp.focus();
                        }

                        if (strLen > 10) {
                            if (debts.Tcard.IndexOf('№') > -1) {
                                debts.Tcard = debts.Tcard.Replace("№", "");
                                inp.focus();
                                inp.value = debts.Tcard;
                            }
                        }
                        //  MessageBox.Show(inp.getAttribute("id"));
                        break;
                    }
                    // debts.Tcard = null;
                }
            
                dynamic a = document.getElementById("button_next").focus();
                dynamic b = document.getElementById("button_next").focus();
                dynamic buttonNext = document.getElementById("button_next").click();
                //buttonNext.focus();
                //buttonNext.click();

                dynamic Che = document.getElementsByClassName("error error-message")[0];
                oops = Che;
                if (oops != "undefined") {
                    //source_HTTP();
                    //source_HTTP();
                    return;
                }

            }

            if (oops == "undefined") {
                Checc = DateTime.Now;
                Checc = Checc.AddSeconds(15);
                nomer = -1;
                But();
            }
        }


        private void button2_Click(object sender, EventArgs e) {
            dynamic document = (JSObject)webControl1.ExecuteJavascriptWithResult("document");
            dynamic Cheсс = document.getElementsByClassName("error error-message")[0];
            string oops = Cheсс;
            if (oops == "undefined") {
                dannie();
            }
            if (oops != "undefined") {
                vvod_and_click();
            }
        }

        private void Form1_Load(object sender, EventArgs e) {
            // vvod_and_click();
            source_HTTP();

        }

        private void timer1_Tick(object sender, EventArgs e) {

        }
    }
}

