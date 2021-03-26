using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace CDWPlanner.Model
{
    public class JsonContent : StringContent
    {
        public JsonContent() : this(new object())
        {
            
        }
        
        public JsonContent(object value) : base(JsonConvert.SerializeObject(value))
        {
            base.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;charset=UTF-8");
        }
    }
}
