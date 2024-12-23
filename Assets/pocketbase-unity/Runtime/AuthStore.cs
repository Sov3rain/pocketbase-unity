using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace PocketBaseSdk
{
    public record AuthStoreEvent(string Token, RecordModel Model)
    {
        public string Token { get; } = Token;
        public RecordModel Model { get; } = Model;
    }

    [Serializable]
    public class AuthStore
    {
        public readonly EventStream<AuthStoreEvent> OnChange = new();

        public string Token { get; private set; }
        public RecordModel Model { get; private set; }

        public bool IsValid()
        {
            if (string.IsNullOrEmpty(Token))
            {
                return false;
            }

            var parts = Token.Split(".");

            if (parts.Length != 3)
            {
                return false;
            }

            // Add padding if necessary
            var tokenPart = parts[1];
            switch (tokenPart.Length % 4)
            {
                case 2:
                    tokenPart += "==";
                    break;
                case 3:
                    tokenPart += "=";
                    break;
            }

            var jsonBytes = Convert.FromBase64String(tokenPart);
            var jsonString = Encoding.UTF8.GetString(jsonBytes);
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);

            long exp;
            var expValue = data["exp"];

            if (expValue is long longExp)
            {
                exp = longExp;
            }
            else if (!long.TryParse(expValue?.ToString(), out exp))
            {
                exp = 0;
            }

            return exp > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public virtual void Save(string newToken, RecordModel newModel)
        {
            Token = newToken;
            Model = newModel;

            OnChange.Invoke(new AuthStoreEvent(Token, Model));
        }

        public virtual void Clear()
        {
            Token = string.Empty;
            Model = null;

            OnChange.Invoke(new AuthStoreEvent(Token, Model));
        }
    }
}