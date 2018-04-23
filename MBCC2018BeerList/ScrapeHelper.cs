using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MBCC2018BeerList
{
    /// <summary>
    /// Provides helper methods for fetching and parsing webpages.
    /// </summary>
    public static class ScrapeHelper
    {
        // ===========================================================================
        // = Constants
        // ===========================================================================
        
        private const String USER_AGENT = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36";

        // ===========================================================================
        // = Public Methods
        // ===========================================================================
        
        public static HtmlNode Parse(String text)
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(text);

            var htmlNode = htmlDocument.DocumentNode;
            return htmlNode;
        }

        public static async Task<HtmlNode> FetchParseAsync(String uri, String proxy = null, Boolean assumeUnicode = false, Int32? timeoutSecs = null,
            Func<string, string> transform = null)
        {
            var webClient = new ScrapeClient(uri, userAgent: USER_AGENT, assumeUnicode: assumeUnicode, proxy: proxy, timeoutSecs: timeoutSecs);
            var html = await webClient.GetPageAsync();

            if (transform != null)
                html = transform(html);

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var htmlNode = htmlDocument.DocumentNode;
            return htmlNode;
        }

        public static async Task<String> FetchAsync(String uri, Boolean assumeUnicode = false)
        {
            var webClient = new ScrapeClient(uri, userAgent: USER_AGENT, assumeUnicode: assumeUnicode);
            var html = await webClient.GetPageAsync();

            return html;
        }
    }
}
