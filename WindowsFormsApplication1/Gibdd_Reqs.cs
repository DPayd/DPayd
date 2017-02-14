using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace DPayed {
    class Gibdd_Reqs {
        public class Division {
            public string divCode;
            public List<Service> services;
        }

        public class Service {
            public string svcId;
            public string svcActive;
            public DateTime svcUpdated;
            public string svcType;
            public string svcAddr;
            public string bank;
            public string rs;
            public string bik;
            public string poluch;
            public string inn;
            public string kpp;
            public string oktmo;
        }

        public class GibddDivisions {
            public Dictionary<string, Division> divisions = new Dictionary<string, Division>();
        }

        public class Req {
            public string ufk;
            public string inn;
            public string kpp;
            public string oktomo;
            public string raschSchet;
            public string bik;
            public string bankPol;
        }

        public SortedList<string, Req> reqs = new SortedList<string, Req>(); // список с реквизитами подразделений

        private bool isNotEmpty(string field) {
            return field != null && !field.Equals("");
        }

        public void initGibddDivisions() {
            GibddDivisions gibddDivisions = JsonConvert.DeserializeObject<GibddDivisions>(File.ReadAllText(@"c:\!\gibdd_divisions.json"));

            foreach (Division division in gibddDivisions.divisions.Values) {
                if (division.services.Count > 0) {
                    Req req = new Req();

                    foreach (Service service in division.services) {
                        if (isNotEmpty(service.inn) && isNotEmpty(service.oktmo) && isNotEmpty(service.kpp)) {
                            req.inn = service.inn;
                            req.oktomo = service.oktmo;
                            req.kpp = service.kpp;

                            req.ufk = service.poluch;
                            req.raschSchet = service.rs;
                            req.bik = service.bik;
                            req.bankPol = service.bank;
                            Console.Out.WriteLine(division.divCode);
                            break;
                        }
                    }
                  reqs.ContainsKey(division.divCode);
                        reqs.Add(division.divCode, req);
                }
            }
        }
    }
}
