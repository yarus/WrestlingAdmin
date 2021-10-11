using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Wrestling.Data;
using Wrestling.Entities;

namespace Wrestling.Integration
{
    public class RosbosApi : IRosbosApi
    {
        private static string _apiUrl = "https://rosbos.ru";

        private string _userName;
        private string _password;

        public bool CheckConnection()
        {
            try
            {
                using (var client = new WebClient())
                using (client.OpenRead(_apiUrl))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public void SetCredentials(string userName, string password)
        {
            _userName = userName;
            _password = password;
        }

        public bool LoadToken()
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"{_apiUrl}/api/data?handler=Auth&userName={_userName}&password={_password}");
                request.Timeout = 5000;
                request.Credentials = CredentialCache.DefaultNetworkCredentials;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error while accessing Integration Api: {ex}");
                return false;
            }

            return false;
        }

        public List<TeamApplication> GetTeams()
        {
            List<TeamApplication> result = new List<TeamApplication>();

            var url = $"{_apiUrl}/api/data?handler=Teams&userName={_userName}&password={_password}";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            //request.Timeout = 5000;
            //request.Credentials = CredentialCache.DefaultNetworkCredentials;

            try
            {
                using (WebResponse jsonResponse = request.GetResponse())
                {
                    var streamReader = new StreamReader(jsonResponse.GetResponseStream() ?? throw new InvalidOperationException());
                    var responseData = streamReader.ReadToEnd();
                    var data = JsonConvert.DeserializeObject<List<TeamApplicationInfo>>(responseData);

                    foreach (var info in data)
                    {
                        var entity = new TeamApplication
                        {
                            City = info.City,
                            Country = info.Country,
                            Email = info.Email,
                            EmblemPath = info.EmblemPath,
                            FullAddress = info.FullAddress,
                            FullName = info.FullName,
                            ID = info.ID,
                            MainCoach = info.MainCoach,
                            PhoneNumber = info.PhoneNumber,
                            Representative = info.Representative,
                            ShortName = info.ShortName,
                            HashTag = info.HashTag
                        };

                        result.Add(entity);
                    }
                }
            }
            catch
            {

            }

            return result;
        }

        public List<Wrestler> GetWrestlers()
        {
            List<Wrestler> result = new List<Wrestler>();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"{_apiUrl}/api/data?handler=Athletes&userName={_userName}&password={_password}");
            //request.Timeout = 10000;
            //request.Credentials = CredentialCache.DefaultNetworkCredentials;

            try
            {
                using (WebResponse jsonResponse = request.GetResponse())
                {
                    var streamReader = new StreamReader(jsonResponse.GetResponseStream() ?? throw new InvalidOperationException());
                    var responseData = streamReader.ReadToEnd();
                    var data = JsonConvert.DeserializeObject<List<WrestlerInfo>>(responseData);

                    foreach (var info in data)
                    {
                        var wrestler = new Wrestler
                        {
                            ID = info.ID,
                            FinalPlace = null,
                            LastName = info.LastName,
                            BirthDate = info.BirthDate,
                            FirstName = info.FirstName,
                            MiddleName = info.MiddleName,
                            Weight = info.Weight,
                            IsFemale = info.IsFemale,
                            IsSeedFixed = false,
                            SeedNumber = info.SeedNumber,
                            GroupID = null,
                            IsEntryFeePaid = false,
                            TeamID = null,
                            HashTag = info.HashTag,
                            PaidAmount = null,
                            IsWeightApproved = false
                        };

                        result.Add(wrestler);
                    }
                }
            }
            catch
            {
            }

            return result;
        }
    }
}