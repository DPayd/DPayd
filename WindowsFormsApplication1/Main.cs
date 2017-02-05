using System;
using System.Windows.Forms;
using Awesomium.Core;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Data.SqlTypes;

namespace debts {
    public partial class CheckDebts : Form {
        public const string DEFAULT_LOG_NAME = "DPayd.log";
        public const string SERVICE_NAME = "DPAYD";
        public const string MSG_WRONG = "Некорректные серия и номер";
        public const string INN_PAYED = "PAYED";
        public const int DAYS_TO_PAY = 70;

        public CheckDebts() {
            InitializeComponent();

        }
        const string SINGLE_SPACE = " ";
        const string DOUBLE_SPACE = "  ";
        const string CLOSING_TAG_P = "</p>";
        const string CLOSING_TAG_SPAN = "</span>";
        public string hTML2;
        public string pravonarushenie;
        public string datas;
        public string tEXT;
        public string mesto;
        public string org_Vlasti;
        public string Summa;
        public string S = "77УЕ093445";
        public string Tecart;
        public int INT;
        public int nomer = -1;
        List<string> Listen = new List<string>();
        Debts debts = new Debts();
        DateTime lastGetUriTime = DateTime.Now;

        // добавлено Nik
        DBProcessor dbProcessor = new DBProcessor();

        string number_only(ref dynamic text) {
           // text = text.ToString();
            string resultat = "";
            for (int i = 0; i < text.Length; i++)
                if ((text[i] >= '0') && (text[i] <= '9'))
                    resultat += text[i];
            tEXT = resultat.Trim(); 
            //MessageBox.Show(tEXT);
            text = tEXT;
            return text;
        }

        int getFirstNum(string text) {
            for (int i = 0; i < text.Length; i++)
                if ((text[i] >= '0') && (text[i] <= '9'))

                    return i;
            return -1;
        }

        async void But() {
            await Task.Delay(1000); // 1 секундa
            process();
        }

        public class Debts {
            //это поля из таблицы Debts, которые заполняются, поэтому я тебе буду давать всю эту структуру, вместо Tcard
            // а ты заполнишь те, которые отмечены * плюс те, которые хотел Барк в дополнение
            public SqlDecimal ID;
            public string Vclstamp;
            public SqlDecimal Vcl;
            public string Tcard;
            public string Reason; //* ?
            public string Ordinance; //
            public DateTime Dbtdte; //*  ?
            public DateTime Ofndte; //*  ?
            public SqlDecimal Sum; //
            public SqlDecimal SumHalf; //
            public DateTime Paytodte; //-
            public DateTime PaytoHalf; //
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

        private Debts Tcarting() {
            // Здесь надо подкл, к БД и получить Tecart.
            Debts nextDebts = new Debts();
            // Передача параметра ПО ЗНАЧЕНИЮ!!!
            if (dbProcessor.readNextTcard(ref nextDebts)) {

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
//                    Tecart = Tcarting().Tcard;
//            if (Tecart != "") {
//                MessageBox.Show(Tecart);
//                vvod_and_click(); // !!!!!!!!!!!!! это неправильно: вызывать функцию из себя же (специальный приём - рекурсия).
//                                  // Но он здесь ни к чему. Кроме того, при рекурсии параметры передаются через стек, который может переполниться (он ограничен)
//                                  // !!!! Используй цикл, например while (Ты понимаешь, что такое функция? Что такое вернуть значение?)
//        Tecart = "";
//            }

//            if (Tecart == "") {
//                MessageBox.Show("Tcard закончИлись");
//            }

//inp.focus();
//                inp.value = "77УЕ093445";
//                int strLen = Tecart.Length;
//                if (strLen == 10) {
//                    inp.value = Tecart;
//                }

//                if (strLen > 10) {
//                    if (Tecart.IndexOf('№') > -1) {
//                        Tecart = Tecart.Replace("№", "");
//                        inp.value = Tecart;
//                    }
//                }

//                //  MessageBox.Show(inp.getAttribute("id"));
//                break;
//            }
//            // Tecart = null;
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

        private void workDB(Debts rec) {
            //Здесь надо положить в ДБ debts.Pravonarushenie и т.д
            if (dbProcessor.check(ref rec))
                dbProcessor.update(rec);
            else
                dbProcessor.insert(rec);
        }

        private void process() {
            dynamic document = (JSObject)webControl1.ExecuteJavascriptWithResult("document");
            dynamic Chect = document.getElementsByClassName("rendered_charge_container")[0];
            string oter = Chect;
            if (oter != "undefined") {
                dannie();
            }
            if (oter == "undefined") {
                But();
            }
        }

        private void Awesomium_Windows_Forms_WebControl_DocumentReady(object sender, DocumentReadyEventArgs e) {
            //MessageBox.Show("Вызов");
            if (e.ReadyState == DocumentReadyState.Loaded)
                vvod_and_click();
        }

        private void source_HTTP() 
        {
            webControl1.Source = new Uri("https://www.mos.ru/pgu/ru/application/gibdd/fines/?utm_source=mos&utm_medium=ek&utm_campaign=85532&utm_term=884533#step_1");
        }

        private void dannie() {
            while(nomer != -10) {
                if (nomer == -10)
                    break;
                dynamic document = (JSObject)webControl1.ExecuteJavascriptWithResult("document");
                nomer++;
                dynamic Chec = document.getElementById("npayed").getElementsByClassName("rendered_charge_container")[nomer];
                string sr = Chec;

                if (sr == "undefined") {
                    nomer = -10;
                    break;
                }

                if (sr == "undefined" && nomer == 0) {
                        MessageBox.Show("Штрафов не нашлось");
                        nomer = -10;
                    break;
                }
                if (sr != "undefined") {
                    dynamic rendConteiner = document.getElementById("npayed").getElementsByClassName("rendered_charge_container")[nomer];
                    string Ordinance = rendConteiner.getAttribute("id");
                    debts.Ordinance = Ordinance;
                    dynamic HTML = document.getElementById("npayed").getElementsByClassName("rendered_charge_container")[nomer].outerHTML;
                    // MessageBox.Show(HTML);
                    //   Clipboard.SetText(HTML);
                    hTML2 = HTML;
                    pravonarushenie = getField(HTML, "Правонарушение");
                    debts.Reason = pravonarushenie;


                    string DataPastanov = pravonarushenie.Substring(pravonarushenie.IndexOf("от") + 2, 12);
                    DataPastanov = DataPastanov.Trim();

                    //MessageBox.Show(DbtDte);

                    datas = getField(HTML, "Дата нарушения");
                    DateTime data = DateTime.Parse(datas);
                    debts.Ofndte = data;

                    debts.Dbtdte = data.AddDays(DAYS_TO_PAY);
                    string gdcgd = debts.Dbtdte.ToString();
                    /*
                                        mesto = getField(HTML, "Место нарушения");      Это не надо
                                        debts.Mesto = mesto;

                                        org_Vlasti = getField(HTML, "Орган власти");
                                        org_Vlasti = org_Vlasti.Replace("&nbsp;","");
                                        org_Vlasti = org_Vlasti.Replace("<br>", "");       Это не надо
                                        org_Vlasti = org_Vlasti.Trim();
                                        debts.Org_vlasti = org_Vlasti;
                    */

                    Summa = getField(HTML, "Штраф");

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
                        string fhdu = "mdu";
                    }
                    if (hTML2.IndexOf("strike") == -1) {
                        //debts.PaytoHalf = ;
                        debts.SumHalf = 0;
                    }
                    }
                //if (sr == "undefined") {
                //    nomer = -10;
                //    MessageBox.Show("Проверка закончена!");
                //    break;
                //    //  source_HTTP();

                //}



            }
        }
        private void vvod_and_click() {
            while (Tecart != "") 
                
            {
                //Ввожу данные
                dynamic document = (JSObject)webControl1.ExecuteJavascriptWithResult("document");
                dynamic inputs = document.getElementsByTagName("input");
                int lenInputs = (int)inputs.length;
                for (int i = 0; i < lenInputs; i++) {
                    dynamic inp = document.getElementsByTagName("input")[i];
                    String inpName = inp.getAttribute("name");
                    if (inpName.Contains("as_values_")) {
                        //Tecart = Tcarting().Tcard;
                        Tecart = "77УЕ093445";
                       // inp.value = Tecart;
                        int strLen = Tecart.Length;
                        if (strLen == 10) {
                            inp.focus();
                            inp.value = Tecart;
                            Tecart = "";
                            //  inp.focus();
                        }

                        if (strLen > 10) {
                            if (Tecart.IndexOf('№') > -1) {
                                Tecart = Tecart.Replace("№", "");
                                inp.value = Tecart;
                            }
                        }
                        //  MessageBox.Show(inp.getAttribute("id"));
                        break;
                    }
                    // Tecart = null;
                }
                dynamic b = document.getElementById("button_next").focus();
                dynamic buttonNext = document.getElementById("button_next").click();
                //buttonNext.focus();
                //buttonNext.click();

                dynamic Che = document.getElementsByClassName("error error-message")[0];
                string oops = Che;
                if (oops != "undefined") {
                    vvod_and_click();
                 }
                But();
            }
            But();
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

