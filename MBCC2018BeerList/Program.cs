using System;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Fizzler.Systems.HtmlAgilityPack;
using System.Web;
using CsvHelper;
using CsvHelper.Configuration;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MBCC2018BeerList
{
    public class Program
    {
        private const string MbccDataUrl = "https://mbcc.dk/beers";
        private const string OutputDir = "MBCC2018BeerList";

        public static IConfigurationRoot Configuration { get; private set; }
        public static ServiceProvider Services { get; private set; }

        public static async Task Main(string[] args)
        {
            // Configure.
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                //.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();

#if DEBUG
            configBuilder = configBuilder.AddUserSecrets<Secrets>();
#endif

            Configuration = configBuilder.Build();

            // Get Windows-1252 encoding.
            var enc1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252);

            // Fetch latest data.
            var data = await FetchDataAsync();

            // Set up data directory.
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var outputDir = Path.Combine(localAppData, OutputDir);
            Directory.CreateDirectory(outputDir);

            // Create file paths.
            var tempFullFilePath = Path.Combine(outputDir, "mbcc-2018-complete.tmp.csv");
            var tempDiffFilePath = Path.Combine(outputDir, "mbcc-2018-diff.tmp.csv");
            var fullFilePath     = Path.Combine(outputDir, "mbcc-2018-complete.csv");
            var prevFullFilePath = Path.Combine(outputDir, "mbcc-2018-complete-prev.csv");
            var diffFilePath     = Path.Combine(outputDir, "mbcc-2018-diff.csv");

            // Delete old temp files.
            File.Delete(tempFullFilePath);
            File.Delete(tempDiffFilePath);

            // Write new data to temp file.
            using (var writer = new CsvWriter(new StreamWriter(tempFullFilePath, false, enc1252)))
            {
                writer.WriteRecords(data.Beers);
            }

            // Load current data.
            IList<MbccBeer> curRecords = null;

            if (File.Exists(fullFilePath))
            {
                using (var textReader = new StreamReader(fullFilePath, enc1252))
                using (var csvReader = new CsvReader(new CsvParser(textReader)))
                {
                    curRecords = csvReader.GetRecords<MbccBeer>().ToList();
                }
            }

            // Diff new data against current data.
            if (curRecords != null && curRecords.Count != 0)
            {
                var newHashSet = new HashSet<MbccBeer>(data.Beers);
                var curHashSet = new HashSet<MbccBeer>(curRecords);

                var addedRecords = newHashSet
                    .Except(curHashSet)
                    .Select(x => MbccBeerDiff.FromBeer(x, AddRemove.Added, DateTime.Now));

                var removedRecords = curHashSet
                    .Except(newHashSet)
                    .Select(x => MbccBeerDiff.FromBeer(x, AddRemove.Removed, DateTime.Now));

                IList<MbccBeerDiff> prevDiffRecords = null;

                // Read current diff data.
                if (File.Exists(diffFilePath))
                    using (var reader = new CsvReader(new StreamReader(diffFilePath, enc1252)))
                    {
                        prevDiffRecords = reader.GetRecords<MbccBeerDiff>().ToList();
                    }

                // Write diff data to temp file.
                // Appending the newest data at the top, then the previous data.
                using (var writer = new CsvWriter(new StreamWriter(tempDiffFilePath, false, enc1252)))
                {
                    writer.WriteRecords(addedRecords);
                    writer.WriteRecords(removedRecords);

                    if (prevDiffRecords != null && prevDiffRecords.Count > 0)
                        writer.WriteRecords(prevDiffRecords);
                }

                // Delete current diff data.
                File.Delete(diffFilePath);

                // Copy new diff data.
                File.Copy(tempDiffFilePath, diffFilePath);
            }

            // Delete oldest data.
            File.Delete(prevFullFilePath);

            // Move current data to previous.
            if (File.Exists(fullFilePath))
                File.Move(fullFilePath, prevFullFilePath);

            // Move new data to current.
            File.Move(tempFullFilePath, fullFilePath);

            // Publish to Azure.
            await PublishDataAsync(new[] { fullFilePath, diffFilePath });
        }

        public static async Task PublishDataAsync(IList<string> paths)
        {
            var connString = Configuration["AzureBlobConnectionString"];

            var account = CloudStorageAccount.Parse(connString);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference("data-dev");

            foreach (var path in paths)
            {
                var fileName = Path.GetFileName(path);
                var azureBlob = container.GetBlockBlobReference(fileName);
                await azureBlob.UploadFromFileAsync(path);
            }
        }

        public static async Task<MbccData> FetchDataAsync()
        {
            var data = new MbccData();
            var html = await ScrapeHelper.FetchParseAsync(MbccDataUrl, assumeUnicode: true);

            var sessions = html.QuerySelectorAll("h2.name");

            foreach (var sessionNode in sessions)
            {
                var sessionName = HttpUtility.HtmlDecode(sessionNode.InnerText).Trim();
                var sessionMatch = Regex.Match(sessionName, "^[A-Za-z]*");
                var session = Enum.Parse<CbcSession>(sessionMatch.Captures[0].Value);

                var items = sessionNode.NextSibling.QuerySelectorAll(".item");

                foreach (var item in items)
                {
                    var beerName    = HttpUtility.HtmlDecode(item.QuerySelector(".name__beer span").InnerText).Trim();
                    var breweryName = HttpUtility.HtmlDecode(item.QuerySelector(".name__brewery span").InnerText).Trim();
                    var style       = HttpUtility.HtmlDecode(item.QuerySelector(".style span").InnerText).Trim();
                    var abvRaw      = HttpUtility.HtmlDecode(item.QuerySelector(".abv").InnerText).Trim();
                    var abvMatch    = Regex.Match(abvRaw, "^([0-9]*\\.?[0-9]*)");
                    var abv         = abvMatch.Captures[0].Value;

                    data.Beers.Add(new MbccBeer
                    {
                        BeerName    = beerName,
                        BreweryName = breweryName,
                        Style       = style,
                        ABV         = abv,
                        Session     = session
                    });
                }
            }

            return data;
        }
    }
}
