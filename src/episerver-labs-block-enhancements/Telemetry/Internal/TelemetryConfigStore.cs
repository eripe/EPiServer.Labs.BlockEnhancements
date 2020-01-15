﻿using EPiServer.Framework.Serialization;
using EPiServer.Licensing;
using EPiServer.Security;
using EPiServer.Shell.Modules;
using EPiServer.Shell.Services.Rest;
using EPiServerProfile = EPiServer.Personalization.EPiServerProfile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace EPiServer.Labs.BlockEnhancements.Telemetry.Internal
{
    [RestStore("telemetryconfig")]
    public class TelemetryConfigStore : RestControllerBase
    {
        private readonly TelemetryOptions _telemetryOptions;
        private readonly LicensingOptions _licensingOptions;
        private readonly IPrincipalAccessor _principalAccessor;
        private readonly ModuleTable _moduleTable;
        private readonly IObjectSerializer _objectSerializer;

        public TelemetryConfigStore(
            TelemetryOptions telemetryOptions,
            LicensingOptions licensingOptions,
            IPrincipalAccessor principalAccessor,
            ModuleTable moduleTable,
            IObjectSerializer objectSerializer)
        {
            _telemetryOptions = telemetryOptions;
            _licensingOptions = licensingOptions;
            _principalAccessor = principalAccessor;
            _moduleTable = moduleTable;
            _objectSerializer = objectSerializer;

            HashHandler = new SiteSecurity();
        }

        [HttpGet]
        public async Task<RestResult> Get()
        {
            if (!_telemetryOptions.IsTelemetryEnabled())
            {
                return Rest(new TelemetryConfigModel
                {
                    Configuration = new Dictionary<string, object>
                    {
                        ["disableTelemetry"] = true
                    }
                });
            }

            return Rest(new TelemetryConfigModel
            {
                Client = GetClientHash(),
                Configuration = await GetTelemetryConfiguration().ConfigureAwait(false),
                User = GetUserHash(),
                Versions = GetVersions()
            });
        }

        private string GetClientHash()
        {
            var licenseKey = _licensingOptions.LicenseKey;
            if (licenseKey != null)
            {
                return HashString(licenseKey);
            }

            try
            {
                var license = LoadLicense(_licensingOptions.LicenseFilePath);
                return HashString(license?.LicensedCompany);
            }
            catch
            {
                return null;
            }
        }

        private Dictionary<string, string> GetVersions()
        {
            return _moduleTable.GetModules().ToDictionary(_ => _.Name, _ => _.ResolveVersion().ToString());
        }

        private async Task<IDictionary<string, object>> GetTelemetryConfiguration()
        {
            var endpointUrl = "https://episervercmsui.azurewebsites.net/api/telemetry-config";

            using (var response = await GetRequestAsync(endpointUrl).ConfigureAwait(false))
            using (var content = response.Content)
            {
                var raw = await content.ReadAsStringAsync().ConfigureAwait(false);
                return _objectSerializer.Deserialize<IDictionary<string, object>>(raw);
            }
        }

        private string GetUserHash()
        {
            var username = _principalAccessor.CurrentName();
            var email = LoadEmailFromProfile(username);
            return HashString(email ?? username);
        }

        private string HashString(string data)
        {
            if (data == null)
            {
                return null;
            }
            return HashHandler.GenerateStringHash(Encoding.Unicode.GetBytes(data)).TrimEnd('=');
        }

        // Delegate get request to allow mocking in unit tests.
        internal Func<string, Task<HttpResponseMessage>> GetRequestAsync = async (string requestUri) => {
            using (var client = new HttpClient())
            {
                return await client.GetAsync(requestUri).ConfigureAwait(false);
            }
        };

        // Allow mocking the generated hash in unit tests.
        internal IHashHandler HashHandler { get; set; }

        // Delegate license loading to allow mocking in unit tests.
        internal Func<string, LicenseData> LoadLicense = (string licenseFilePath) =>
        {
            var key = RSA.Create();
            key.FromXmlString(CloudLicenseConsts.PublicKey);
            return LicenseData.Load(licenseFilePath, null, key).FirstOrDefault();
        };

        // Delegate profile loading to allow mocking in unit tests.
        internal Func<string, string> LoadEmailFromProfile = (string username) => EPiServerProfile.Get(username)?.Email;
    }
}
