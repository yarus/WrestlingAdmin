using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wrestling.Data;

namespace Wrestling.DataAccess
{
    // Pre-rename .wrt files used "Carpets" / "CarpetID" / "CarpetLabel" before
    // these were renamed to "Mats" / "MatID" / "MatLabel". Map the legacy keys
    // to the new ones at deserialize time so old tournament files still load.
    // Write path is unchanged — we always emit the new names.
    public class LegacyMatNameConverter : JsonConverter
    {
        // Inner serializer has no converters: prevents recursion when we re-bind
        // the migrated JObject back into a strongly-typed instance. Uses the
        // same hardened settings as the storage layer (TypeNameHandling.None).
        private static readonly JsonSerializer _inner = JsonStorageDataAccess.CreateSerializer();

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TournamentInfo)
                   || objectType == typeof(AgeWeightGroupInfo);
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            var jo = JObject.Load(reader);

            if (objectType == typeof(TournamentInfo))
            {
                MigrateTournamentInfo(jo);
            }
            else if (objectType == typeof(AgeWeightGroupInfo))
            {
                MigrateAgeWeightGroupInfo(jo);
            }

            return jo.ToObject(objectType, _inner);
        }

        private static void MigrateTournamentInfo(JObject jo)
        {
            if (jo["Mats"] == null && jo["Carpets"] != null)
            {
                jo["Mats"] = jo["Carpets"];
            }
            jo.Remove("Carpets");

            // Inner serializer has no converters, so AgeWeightGroupInfo entries
            // nested under "Groups" must be migrated up-front here.
            if (jo["Groups"] is JArray groups)
            {
                foreach (var g in groups.OfType<JObject>())
                {
                    MigrateAgeWeightGroupInfo(g);
                }
            }
        }

        private static void MigrateAgeWeightGroupInfo(JObject g)
        {
            if (g["MatID"] == null && g["CarpetID"] != null) g["MatID"] = g["CarpetID"];
            if (g["MatLabel"] == null && g["CarpetLabel"] != null) g["MatLabel"] = g["CarpetLabel"];
            g.Remove("CarpetID");
            g.Remove("CarpetLabel");
        }
    }
}
