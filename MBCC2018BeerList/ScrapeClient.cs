using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MBCC2018BeerList
{
    /// <summary>
    /// Found somewhere on the Internet. Re-encodes responses based on the charset meta.
    /// </summary>
    public class ScrapeClient
    {
        // ===========================================================================
        // = Public Properties
        // ===========================================================================

        public Encoding Encoding { get; set; }
        public WebHeaderCollection Headers { get; set; }
        public Uri Url { get; set; }

        // ===========================================================================
        // = Private Fields
        // ===========================================================================

        private readonly string _referer;
        private readonly string _userAgent;
        private readonly Boolean _assumeUnicode;

        private String _proxy;
        private Int32? _timeoutSecs;

        // ===========================================================================
        // = Construction
        // ===========================================================================
        
        public ScrapeClient(string url, string referer = null, string userAgent = null, String proxy = null, Boolean assumeUnicode = false, Int32? timeoutSecs = null)
        {
            Encoding = Encoding.GetEncoding("ISO-8859-1");
            Url = new Uri(url);

            _userAgent = userAgent;
            _referer = referer;
            _assumeUnicode = assumeUnicode;
            _proxy = proxy;
            _timeoutSecs = timeoutSecs;
        }

        // ===========================================================================
        // = Public Methods
        // ===========================================================================
        
        public async Task<T> GetObjectAsync<T>()
        {
            var str = await Task.Factory.StartNew(() => GetPage());
            var obj = JsonConvert.DeserializeObject<T>(str);

            return obj;
        }

        public async Task<string> GetPageAsync()
        {
            return await Task.Factory.StartNew(() => GetPage());
        }

        public string GetPage()
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);

            if (!String.IsNullOrWhiteSpace(_proxy))
                request.Proxy = new WebProxy(_proxy);

            if (_timeoutSecs.HasValue)
            {
                request.Timeout = _timeoutSecs.Value * 1000;
                request.ReadWriteTimeout = _timeoutSecs.Value * 1000;
            }

            if (!string.IsNullOrEmpty(_referer))
                request.Referer = _referer;
            if (!string.IsNullOrEmpty(_userAgent))
                request.UserAgent = _userAgent;

            request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip");

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                Headers = response.Headers;
                Url = response.ResponseUri;
                return ProcessContent(response);
            }
        }

        // ===========================================================================
        // = Private Methods
        // ===========================================================================
        
        private string ProcessContent(HttpWebResponse response)
        {
            SetEncodingFromHeader(response);

            Stream s = response.GetResponseStream();
            if (response.ContentEncoding.ToLower().Contains("gzip"))
                s = new GZipStream(s, CompressionMode.Decompress);
            //else if (response.ContentEncoding.ToLower().Contains("deflate"))
            //    s = new DeflateStream(s, CompressionMode.Decompress);

            MemoryStream memStream = new MemoryStream();
            int bytesRead;
            byte[] buffer = new byte[0x1000];
            for (bytesRead = s.Read(buffer, 0, buffer.Length); bytesRead > 0; bytesRead = s.Read(buffer, 0, buffer.Length))
            {
                memStream.Write(buffer, 0, bytesRead);
            }
            s.Close();
            string html;
            memStream.Position = 0;
            using (StreamReader r = new StreamReader(memStream, Encoding))
            {
                html = r.ReadToEnd().Trim();
                html = CheckMetaCharSetAndReEncode(memStream, html);
            }

            return html;
        }

        private void SetEncodingFromHeader(HttpWebResponse response)
        {
            string charset = null;
            if (string.IsNullOrEmpty(response.CharacterSet))
            {
                Match m = Regex.Match(response.ContentType, @";\s*charset\s*=\s*(?<charset>.*)", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    charset = m.Groups["charset"].Value.Trim(new[] { '\'', '"' });
                }
            }
            else
            {
                charset = response.CharacterSet;
            }
            if (!string.IsNullOrEmpty(charset))
            {
                try
                {
                    Encoding = Encoding.GetEncoding(charset);
                }
                catch (ArgumentException)
                {
                }
            }
        }

        private string CheckMetaCharSetAndReEncode(Stream memStream, string html)
        {
            String charset = null;

            if (_assumeUnicode)
                charset = "utf-8";
            else
            {
                Match m = new Regex(@"<meta\s+.*?charset\s*=\s*(?<charset>[A-Za-z0-9_-]+)", RegexOptions.Singleline | RegexOptions.IgnoreCase).Match(html);

                if (m.Success)
                {
                    charset = m.Groups["charset"].Value.ToLower() ?? "iso-8859-1";
                    if ((charset == "unicode") || (charset == "utf-16"))
                        charset = "utf-8";
                }
            }

            if (charset != null)
                try
                {
                    Encoding metaEncoding = Encoding.GetEncoding(charset);
                    if (Encoding != metaEncoding)
                    {
                        memStream.Position = 0L;
                        StreamReader recodeReader = new StreamReader(memStream, metaEncoding);
                        html = recodeReader.ReadToEnd().Trim();
                        recodeReader.Close();
                    }
                }
                catch (ArgumentException) { }

            return html;
        }
    }
}
