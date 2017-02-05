using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Xml.Serialization;
using static debts.CheckDebts;

namespace debts {
    public class Peremennie {
        public string srvName = "192.168.1.90\\Standart2000";
        public string dbName = "Autopark_PVM";
        public string Brn = "1";
        public string makeConnString() {
            return "Data Source=" + srvName + ";Persist Security Info=True;Password=sdftrg;User ID=Nik;Initial Catalog=" + dbName;
        }
    }

    // класс для доступа к БД
    public class DBProcessor {
        private const string INI_FILE_NAME = "DPayd.xml";

        public Config config;
        string lastDbName;
        string lastVclstamp;

        string restartDbName = "";
        string restartVclstamp = "";

        private SqlConnection conn = null;
        string connStr = "";

        public enum State {
            Normal,
            Succefull,
            ReadError,
            ConnectionError,
            ExecSqlError
        }

        public State state = State.Normal;
        Peremennie configSection;
        List<Peremennie> configList = new List<Peremennie>();

        // переменные для хранения текущего списка автомобилей
        List<Debt> debts = new List<Debt>();
        int currentIndex = 0;
        int debtsSize = 0;
        bool debtsEnd() {
            // достигли конца текущего списка атовмобилей
            return currentIndex >= debtsSize;
        }

        // конструктор: в нём идёт только чтение конфигурации
        public DBProcessor() {
            readConfig();
        }

        public class Config {
            public string lastDbName = "";
            public string lastVclstamp = "";
            public List<Peremennie> peremennie = new List<Peremennie>();
        }

        private void readConfig() {
            /*
            using (Stream stream = new FileStream(INI_FILE_NAME, FileMode.Create)) {
                XmlSerializer serializer = new XmlSerializer(typeof(Config));
                config = new Config();
                config.lastDbName = "lastDbName";
                config.lastVclstamp = "lastVclstamp";
                Peremennie p = new Peremennie();
                p.Brn = "1,2,3";
                p.dbName = "dbName";
                p.srvName = "srvName";
                config.peremennie.Add(p);
                config.peremennie.Add(p);
                config.peremennie.Add(p);

                serializer.Serialize(stream, config);
                stream.Close();
            }
            */
            using (Stream stream = new FileStream(INI_FILE_NAME, FileMode.Open)) {
                XmlSerializer serializer = new XmlSerializer(typeof(Config));
                config = (Config)serializer.Deserialize(stream);
                configList.Clear();
                foreach (Peremennie perem in config.peremennie) {
                    configList.Add(perem);
                }

                restartDbName = config.lastDbName;
                restartVclstamp = config.lastVclstamp;

                if (!restartDbName.Equals("")) {
                    // перемещение к базе данных для рестарта
                    while (configList.Count > 0) {
                        if (configList[0].dbName.Trim().Equals(restartDbName.Trim()))
                            break;
                        configList.RemoveAt(0);
                    }
                }
            }
        }

        private void writeConfig() {
            using (Stream stream = new FileStream(INI_FILE_NAME, FileMode.Create)) {
                XmlSerializer serializer = new XmlSerializer(typeof(Config));
                config.lastDbName = lastDbName;
                config.lastVclstamp = lastVclstamp;
                serializer.Serialize(stream, config);
                stream.Close();
            }
        }

        // закрытие ранее открытого SqlConnection и чистка существующего списка автомобилей
        private void closeCurrentConnection() {
            debts.Clear();
            connStr = "";
            if (conn != null)
                conn.Close();
            conn = null;
        }

        public bool connect2NextDBAndReadAll() {
            // закрытие ранее открытых SqlDataReader и SqlConnection
            closeCurrentConnection();

            // соединение со следующей БД
            if (configList.Count == 0) {
                state = State.Succefull;
                return false;
            }

            state = State.ConnectionError;

            configSection = configList[0];
            configList.RemoveAt(0);

            connStr = configSection.makeConnString();
            conn = new SqlConnection(connStr);
            conn.Open();

            lastDbName = configSection.dbName;
            lastVclstamp = "";

            state = State.ExecSqlError;

            string sql =
                "SELECT a.*, b.Des40 AS Brnname " +
                "FROM Vclmst a INNER JOIN " +
                "Brnmst b ON b.Brn = a.Brn " +
                "WHERE (a.Brn IN (" + configSection.Brn + ")) AND (Vclstamp > '" + restartVclstamp + "')" +
                //"      AND(a.Rcdsts < 9) " +
                "ORDER BY a.Vclstamp";

            SqlCommand sqlReadDB = new SqlCommand(sql, conn);
            SqlDataReader sdr = sqlReadDB.ExecuteReader();

            state = State.ReadError;

            while (sdr.Read()) {
                Debt debt = new Debt();
                debt.Vclstamp = readerGetString(sdr, "Vclstamp");
                debt.Vcl = readerGetNumeric(sdr, "Vcl");
                debt.Tcard = readerGetString(sdr, "Tcard").Trim();
                debt.Brn = readerGetNumeric(sdr, "Brn");
                debt.Brnname = readerGetString(sdr, "Brnname").Trim();
                debt.Regnum = readerGetString(sdr, "Regnum").Trim();
                debts.Add(debt);
            }
            sdr.Close();

            state = State.Normal;

            return true;
        }

        public string readerGetString(SqlDataReader sdr, string fieldName) {
            return sdr.GetString(sdr.GetOrdinal(fieldName));
        }

        public DateTime readerGetDate(SqlDataReader sdr, string fieldName) {
            return sdr.GetDateTime(sdr.GetOrdinal(fieldName));
        }

        public SqlDecimal readerGetNumeric(SqlDataReader sdr, string fieldName) {
            return sdr.GetSqlDecimal(sdr.GetOrdinal(fieldName));
        }

        // вызывается для чтения следущей записи (параметр varTcard передаётся по ссылке!!!)
        public bool getNextTcard(ref Debt varRecord) {
            if (conn == null)
                // первый вызов...
                if (!connect2NextDBAndReadAll())
                    // если закончились базы данных, то завершаем
                    return false;

            while (!debtsEnd()) {
                // дошли до конца списка...
                if (!connect2NextDBAndReadAll())
                    // если закончились базы данных, то завершаем
                    return false;
            }

            // присваиваем значения
            varRecord = debts[currentIndex++];

            return true;
        }

        // запись информации о штрафе
        public bool update(Debt rec) {
            if (conn == null)
                return false;
            if (rec.ID < 0)
                return false;

            string sqlUpdate =
                "UPDATE Debts " +
                "SET " +
                "Vclstamp = @Vclstamp, Vcl = @Vcl, Tcard = @Tcard, Reason = @Reason, Dbtdte = @Dbtdte, Ofndte = @Ofndte, " +
                "Sum = @Sum, SumHalf = @SumHalf, Paytodte = @Paytodte, PaytoHalf = @PaytoHalf, " +
                "Brn = @Brn, Brnname = @Brnname, Regnum = @Regnum, Place = @Place, " +
                "Lstchgby = @Lstchgby, Lstchgdte = @Lstchgdte " +
                "WHERE ID = @ID";

            using (SqlConnection connExec = new SqlConnection(connStr)) {
                SqlCommand cmdUpdate = new SqlCommand(sqlUpdate, connExec);
                cmdUpdate.Parameters.AddWithValue("@ID", rec.ID);
                cmdUpdate.Parameters.AddWithValue("@Vclstamp", rec.Vclstamp);
                cmdUpdate.Parameters.AddWithValue("@Vcl", rec.Vcl);
                cmdUpdate.Parameters.AddWithValue("@Tcard", rec.Tcard);
                cmdUpdate.Parameters.AddWithValue("@Reason", rec.Reason);
                cmdUpdate.Parameters.AddWithValue("@Dbtdte", rec.Dbtdte);
                cmdUpdate.Parameters.AddWithValue("@Ofndte", rec.Ofndte);
                cmdUpdate.Parameters.AddWithValue("@Sum", rec.Sum);
                cmdUpdate.Parameters.AddWithValue("@SumHalf", rec.SumHalf);
                cmdUpdate.Parameters.AddWithValue("@Paytodte", rec.Paytodte);
                cmdUpdate.Parameters.AddWithValue("@PaytoHalf", rec.PaytoHalf);
                cmdUpdate.Parameters.AddWithValue("@Brn", rec.Brn);
                cmdUpdate.Parameters.AddWithValue("@Brnname", rec.Brnname);
                cmdUpdate.Parameters.AddWithValue("@Regnum", rec.Regnum);
                cmdUpdate.Parameters.AddWithValue("@Place", rec.Place);

                // установка времени и кем изменена запись
                cmdUpdate.Parameters.AddWithValue("@Lstchgby", SERVICE_NAME);
                cmdUpdate.Parameters.AddWithValue("@Lstchgdte", DateTime.Now);

                connExec.Open();
                int res = cmdUpdate.ExecuteNonQuery();
                connExec.Close();

                bool result = res > 0;
                if (!result) {
                    state = State.ExecSqlError;
                } else {
                    lastVclstamp = rec.Vclstamp;
                    writeConfig();
                }

                return result;
            }
        }

        public bool insert(Debt rec) {
            if (conn == null)
                return false;

            string sqlInsert =
                "INSERT INTO Debts (" +
                "Vclstamp, Vcl, Tcard, Reason, Ordinance, Dbtdte, Ofndte, " +
                "Sum, SumHalf, Paytodte, PaytoHalf, Brn, Brnname, Regnum, Place, " +
                "Entby, Entdte, Lstchgby, Lstchgdte" +
                ") " +
                "VALUES(" +
                "@Vclstamp, @Vcl, @Tcard, @Reason, @Ordinance, @Dbtdte, @Ofndte, " +
                "@Sum, @SumHalf, @Paytodte, @PaytoHalf, @Brn, @Brnname, @Regnum, @Place, " +
                "@Entby, @Entdte, @Lstchgby, @Lstchgdte" +
                ")";

            using (SqlConnection connExec = new SqlConnection(connStr)) {
                SqlCommand cmdInsert = new SqlCommand(sqlInsert, connExec);
                cmdInsert.Parameters.AddWithValue("@Vclstamp", rec.Vclstamp);
                cmdInsert.Parameters.AddWithValue("@Vcl", rec.Vcl);
                cmdInsert.Parameters.AddWithValue("@Tcard", rec.Tcard);
                cmdInsert.Parameters.AddWithValue("@Reason", rec.Reason);
                cmdInsert.Parameters.AddWithValue("@Ordinance", rec.Ordinance);
                cmdInsert.Parameters.AddWithValue("@Dbtdte", rec.Dbtdte);
                cmdInsert.Parameters.AddWithValue("@Ofndte", rec.Ofndte);
                cmdInsert.Parameters.AddWithValue("@Sum", rec.Sum);
                cmdInsert.Parameters.AddWithValue("@SumHalf", rec.SumHalf);
                cmdInsert.Parameters.AddWithValue("@Paytodte", rec.Paytodte);
                cmdInsert.Parameters.AddWithValue("@PaytoHalf", rec.PaytoHalf);
                cmdInsert.Parameters.AddWithValue("@Brn", rec.Brn);
                cmdInsert.Parameters.AddWithValue("@Brnname", rec.Brnname);
                cmdInsert.Parameters.AddWithValue("@Regnum", rec.Regnum);
                cmdInsert.Parameters.AddWithValue("@Place", rec.Place);

                // установка времени и кем добавлена/изменена запись
                cmdInsert.Parameters.AddWithValue("@Entby", SERVICE_NAME);
                cmdInsert.Parameters.AddWithValue("@Entdte", DateTime.Now);
                cmdInsert.Parameters.AddWithValue("@Lstchgby", SERVICE_NAME);
                cmdInsert.Parameters.AddWithValue("@Lstchgdte", DateTime.Now);

                connExec.Open();
                int res = cmdInsert.ExecuteNonQuery();
                connExec.Close();

                bool result = res > 0;
                if (!result) {
                    state = State.ExecSqlError;
                } else {
                    lastVclstamp = rec.Vclstamp;
                    writeConfig();
                }

                return result;
            }
        }

        // проверка на наличие записи с указанным номером постановления в БД
        public bool check(ref Debt rec) {
            if (conn == null)
                return false;

            string sql = "SELECT * FROM Debts WHERE Ordinance = @Ordinance";

            using (SqlCommand sqlReadDB = new SqlCommand(sql, conn)) {
                sqlReadDB.Parameters.AddWithValue("@Ordinance", rec.Ordinance);
                SqlDataReader sdrCheck = sqlReadDB.ExecuteReader();

                bool result = sdrCheck.Read();
                if (result) {
                    rec.ID = readerGetNumeric(sdrCheck, "ID");
                }
                sdrCheck.Close();

                if (!result)
                    rec.ID = -1;

                return result;
            }
        }

    }
}
