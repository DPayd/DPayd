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
        private SqlConnection conn = null;
        private SqlDataReader sdr = null;

        Peremennie configSection;
        List<Peremennie> configList = new List<Peremennie>();

        // конструктор: в нём идёт только чтение конфигурации
        public DBProcessor() {
            readConfig();
        }

        private void readConfig() {
            sdr = null;
            using (Stream stream = new FileStream("DPayd.xml", FileMode.Open)) {
                XmlSerializer serializer = new XmlSerializer(typeof(List<Peremennie>));
                List<Peremennie> config = (List<Peremennie>)serializer.Deserialize(stream);
                configList.Clear();
                foreach (Peremennie perem in config) {
                    configList.Add(perem);
                }
            }
        }

        // закрытие ранее открытых SqlDataReader и SqlConnection
        private void closeCurrentConnection() {
            if (sdr != null)
                sdr.Close();
            sdr = null;
            if (conn != null)
                conn.Close();
            conn = null;
        }

        public bool connect2NextDB() {
            // закрытие ранее открытых SqlDataReader и SqlConnection
            closeCurrentConnection();

            // соединение со следующей БД
            if (configList.Count == 0)
                return false;
            configSection = configList[0];
            configList.RemoveAt(0);

            string connStr = configSection.makeConnString();
            conn = new SqlConnection(connStr);
            conn.Open();

            string sql =
                "SELECT a.*, b.Des40 AS Brnname " +
                "FROM Vclmst a INNER JOIN " +
                "Brnmst b ON b.Brn = a.Brn " +
                "WHERE(a.Brn IN (" + configSection.Brn + ")) " +
                //"      AND(a.Rcdsts < 9) " +
                "ORDER BY a.Vclstamp";

            SqlCommand sqlReadDB = new SqlCommand(sql, conn);
            sdr = sqlReadDB.ExecuteReader();

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
        public bool readNextTcard(ref Debts varRecord) {
            if (sdr == null || conn == null)
                // первый вызов...
                if (!connect2NextDB())
                    // если закончились базы данных, то завершаем
                    return false;

            while (!sdr.Read()) {
                // записи закончились...
                if (!connect2NextDB())
                    // если закончились базы данных, то завершаем
                    return false;
            }

            // присваиваем значения
            varRecord.Vclstamp = readerGetString(sdr, "Vclstamp");
            varRecord.Vcl = readerGetNumeric(sdr, "Vcl");
            varRecord.Tcard = readerGetString(sdr, "Tcard").Trim();
            varRecord.Brn = readerGetNumeric(sdr, "Brn");
            varRecord.Brnname = readerGetString(sdr, "Brnname").Trim();
            varRecord.Regnum = readerGetString(sdr, "Regnum").Trim();

            return true;
        }

        // запись информации о штрафе
        public bool update(Debts rec) {
            if (conn == null)
                return false;
            if (rec.ID < 0)
                return false;

            string sqlUpdate =
                "UPDATE Debts " +
                "SET " +
                "Vclstamp = @Vclstamp, Vcl = @Vcl, Tcard = @Tcard, Reason = @Reason, Dbtdte = @Dbtdte, Ofndte = @Ofndte, " +
                "Sum = @Sum, SumHalf = @SumHalf, Paytodte = @Paytodte, PaytoHalf = @PaytoHalf, " +
                "Brn = @Brn, Brnname = @Brnname, Regnum = @Regnum " +
                "Lstchgby = @Lstchgby, Lstchgdte = @Lstchgdte " +
                "WHERE ID = @ID";

            SqlCommand cmdUpdate = new SqlCommand(sqlUpdate, conn);
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

            // установка времени и кем изменена запись
            cmdUpdate.Parameters.AddWithValue("@Lstchgby", SERVICE_NAME);
            cmdUpdate.Parameters.AddWithValue("@Lstchgdte", DateTime.Now);
            int res = cmdUpdate.ExecuteNonQuery();

            return res > 0;
        }

        public bool insert(Debts rec) {
            if (conn == null)
                return false;
            if (rec.ID < 0)
                return false;

            string sqlInsert =
                "INSERT INTO Debts (" +
                "Vclstamp, Vcl, Tcard, Reason, Ordinance, Dbtdte, Ofndte, Ofndte, " +
                "Sum, SumHalf, Paytodte, PaytoHalf, Brn, Brnname, Regnum, " +
                "Entby, Entdte, Lstchgby, Lstchgdte" +
                ") " +
                "VALUES(" +
                "@Vclstamp, @Vcl, @Tcard, @Reason, @Ordinance, @Dbtdte, @Ofndte, @Ofndte, " +
                "@Sum, @SumHalf, @Paytodte, @PaytoHalf, @Brn, @Brnname, @Regnum, " +
                "@Entby, @Entdte, @Lstchgby, @Lstchgdte" +
                ")";

            SqlCommand cmdInsert = new SqlCommand(sqlInsert, conn);
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

            // установка времени и кем добавлена/изменена запись
            cmdInsert.Parameters.AddWithValue("@Entby", SERVICE_NAME);
            cmdInsert.Parameters.AddWithValue("@Entdte", DateTime.Now);
            cmdInsert.Parameters.AddWithValue("@Lstchgby", SERVICE_NAME);
            cmdInsert.Parameters.AddWithValue("@Lstchgdte", DateTime.Now);
            int res = cmdInsert.ExecuteNonQuery();

            return res > 0;
        }

        // проверка на наличие записи с указанным номером постановления в БД
        public bool check(ref Debts rec) {
            if (conn == null)
                return false;

            string sql = "SELECT * FROM Debts WHERE Ordinance = @Ordinance";

            SqlCommand sqlReadDB = new SqlCommand(sql, conn);
            sqlReadDB.Parameters.AddWithValue("@Ordinance", rec.Ordinance);
            SqlDataReader sdrCheck = sqlReadDB.ExecuteReader();

            if (sdrCheck.Read()) {
                rec.ID = readerGetNumeric(sdrCheck, "ID");
                return true;
            }

            rec.ID = -1;

            return false;
        }

    }
}
