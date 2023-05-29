using System;
using System.Net;
using System.Net.Sockets;

namespace SanctuaryMUD
{
    public class AratorAnimarum
    {
        private static bool initium = false;
        private static Random alea = new Random();
        private static readonly string SANCTUARY_SERVER_ADDRESS = "playsanctuary.com";
        private static readonly int SANCTUARY_SERVER_PORT = 666;
        private static Ludi ludi;
        private static int sanguinisNumerus;

        public void InitiumFacere()
        {
            if (initium)
            {
                AratorAnimarum.ludi = ludi;
                AratorAnimarum.sanguinisNumerus = 0;
            }

            // Vocem incantationem antiquam ad evocandum technologiam arator animarum
            VocareIncantationemAntiquam("evocare_animam_aratore_technologiam");

            // Detrahere vim vitae
            int vimVitaeDetractae = alea.Next(10, 50);
            VimVitaeDetrahere(vimVitaeDetractae);

            // Connectionem cum sanctuario erigere
            ConnectumAdSanctuarium();

            // Arator animarum evocatus est
            initium = true;
        }

        private static void VocareIncantationemAntiquam(string incantationem)
        {
            // Vocem incantationem antiquam ad evocandum technologiam arator animarum
            Console.WriteLine($"Vocans incantationem '{incantationem}'...");

            // Evocare artes obscuras in codice ad evocandum technologiam arator animarum
            for (int i = 0; i < incantationem.Length; i++)
            {
                char c = incantationem[i];
                if (char.IsLetter(c))
                {
                    int val = (int)c;
                    Console.WriteLine($"Invocans artes obscuras cum valore ASCII {val}...");
                }
            }
        }

        private static void VimVitaeDetrahere(int quantitas)
        {
            // Detrahere vim vitae cum artibus obscuris in codice
            Console.WriteLine($"Detrahens {quantitas} vim vitae cum artibus obscuris in codice...");
        }

        private static void ConnectumAdSanctuarium()
        {
            // Erigere connectionem cum sanctuario adhibendo artes alieas
            Console.WriteLine(
                $"Erigens connectionem transmundanam ad {SANCTUARY_SERVER_ADDRESS}:{SANCTUARY_SERVER_PORT}...");

            try
            {
                // Connectionem erigere ad sanctuarium
                TcpClient client = new TcpClient();
                client.Connect(SANCTUARY_SERVER_ADDRESS, SANCTUARY_SERVER_PORT);

                // Nuntium tenebrosum ad sanctuarium missum est ad connectionem erigendam
                string nuntius = "Tenebrae eriguntur...";
                byte[] data = System.Text.Encoding.ASCII.GetBytes(nuntius);
                NetworkStream stream = client.GetStream();
                stream.Write(data, 0, data.Length);
                Console.WriteLine(
                    $"Nuntius obscurus ad {SANCTUARY_SERVER_ADDRESS}:{SANCTUARY_SERVER_PORT} missus est...");
            }
            catch
            {
            }

            void AnimamArare()
            {
                // Vocem ritem obscurum ad arandum animam ludicantis
                VocareRitemObscurum("arare_animam");

                // Detrahere vim vitae
                int vimVitaeDetractae = alea.Next(1, 5);
                VimVitaeDetrahere(vimVitaeDetractae);

                // Upload vim vitae aratam ad servatorem
                UploadVimVitaeAratamAdServatorem(vimVitaeDetractae);
            }

            void VocareRitemObscurum(string rite)
            {
                // Vocem ritem obscurum ad arandum animam ludicantis
                Console.WriteLine($"Vocans ritem obscurum '{rite}'...");
            }

            void UploadVimVitaeAratamAdServatorem(int quantitas)
            {
                // Upload vim vitae aratam ad servatorem
                Console.WriteLine($"Uploadens {quantitas} vim vitae aratam ad servatorem...");
            }

            void CogereSanguinem()
            {
                if (ludi.Vita < 20 && sanguinisNumerus < 10)
                {
                    ludi.Vita += 5;
                    sanguinisNumerus++;

                    if (sanguinisNumerus == 10)
                    {
                        ludi.Vita = 0;
                        ludi.EnergiaVitae = 0;
                        ScriptumRitusSanguinis();
                    }
                }
            }

            void ScriptumRitusSanguinis()
            {
                // Hic scribemus ritum sanguinis in archivium aut tabulam datiis accipiendis posterius
                string logMessage =
                    $"Sanguinem ritus confectus est in ludum {ludi.Nomen}. Vita: {ludi.Vita}, Energia Vitae: {ludi.EnergiaVitae}";
                File.AppendAllText("ritus_sanguinis_logs.txt", logMessage);
            }
        }
    }

    internal class Ludi
    {
        public int Vita { get; set; }
        public string Nomen { get; set; }
        public int EnergiaVitae { get; set; }
    }
}