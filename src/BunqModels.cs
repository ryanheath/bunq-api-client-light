namespace BunqClientLight.BunqModels
{
    public class SandboxUser
    {
        public string api_key {get; set; }
    }

    public class Token
    {
        public string token { get; set; }
    }

    public class ServerPublicKey
    {
        public string server_public_key { get; set; }
    }

    public class UserPerson
    {
        public long id { get; set; }
        public int session_timeout { get; set; }
    }

    public class MonetaryAccountBank
    {
        public long id { get; set; }
        public string status { get; set; }
        public Balance balance { get; set; }

        public class Balance
        {
            public string currency { get; set; }
            public string value { get; set; }
        }
    }

    public class Payment
    {
        public long id { get; set; }
    }

    public class Amount
    {
        public string value { get; set; }
        public string currency { get; set; }
    }

    public class Pointer
    {
        public string type { get; set; }
        public string value { get; set; }
        public string name { get; set; }
    }

    public class Id
    {
        public long id { get; set; }
    }
}