//using Microsoft.Extensions.Localization;
//using Newtonsoft.Json;
//using System.Globalization;

//namespace IndividualAuthorizationSahelWorker
//{
//    public class JsonLocalization
//    {
//        public string Key { get; set; }
//        public Dictionary<string, string> LocalizedValue = new Dictionary<string, string>();
//    }

//    public class JsonStringLocalizerFactory : IStringLocalizerFactory
//    {
//        public IStringLocalizer Create(Type resourceSource)
//        {
//            return new JsonStringLocalizer();
//        }

//        public IStringLocalizer Create(string baseName, string location)
//        {
//            return new JsonStringLocalizer();
//        }
//    }

//    public class JsonStringLocalizer : IStringLocalizer
//    {
//        protected List<JsonLocalization> _localization = new List<JsonLocalization>();

//        public JsonStringLocalizer()
//        {
//            //read all json file
//            JsonSerializer serializer = new JsonSerializer();
//            _localization = JsonConvert.DeserializeObject<List<JsonLocalization>>(File.ReadAllText(@"Resources/localization.json"));
//        }

//        public LocalizedString this[string key]
//        {
//            get
//            {
//                var value = GetString(key);
//                return new LocalizedString(key, value ?? key, resourceNotFound: value == null);
//            }
//        }

//        public LocalizedString this[string key, params object[] arguments]
//        {
//            get
//            {
//                var format = GetString(key);
//                var value = string.Format(format ?? key, arguments);
//                return new LocalizedString(key, value, resourceNotFound: format == null);
//            }
//        }

//        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
//        {
//            return _localization.Where(l => l.LocalizedValue.Keys.Any(lv => lv == CultureInfo.CurrentCulture.Name)).Select(l => new LocalizedString(l.Key, l.LocalizedValue[CultureInfo.CurrentCulture.Name], true));
//        }

//        public IStringLocalizer WithCulture(CultureInfo culture)
//        {
//            return new JsonStringLocalizer();
//        }

//        private string GetString(string key)
//        {
//            var query = _localization.Where(l => l.LocalizedValue.Keys.Any(lv => lv == CultureInfo.CurrentCulture.Name));
//            var value = query.FirstOrDefault(l => l.Key == key);
//            if (value != null)
//            {
//                return value.LocalizedValue[CultureInfo.CurrentCulture.Name];
//            }
//            return key;
//        }
//    }
//}