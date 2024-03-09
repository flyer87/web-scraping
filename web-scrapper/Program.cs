using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using static System.Net.Mime.MediaTypeNames;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();
    private static readonly string baseUrl = "https://books.toscrape.com/";
    private static readonly byte MAX_PAGES = 2;
    private static int currentPage = 0;

    static async Task Main()
    {
        await TraversePagesAsync("index.html");
        Console.WriteLine("Scraping completed!");

    }


    private static async Task TraversePagesAsync(string relativeUrl) {

        string fullUrl = baseUrl + relativeUrl;

        Console.WriteLine($"Processing: {fullUrl}");

        using (HttpClient client = new())
        {
            HttpResponseMessage response = await client.GetAsync(fullUrl);
            string htmlContent = await response.Content.ReadAsStringAsync();

            HtmlDocument htmlDoc = new ();

            // Load the HTML content into an HtmlDocument
            htmlDoc.LoadHtml(htmlContent);

            // create the page folder
            var pageFolder = ExtractPartBeforeHtml(relativeUrl);
            // string outputFolder = "downloaded_pages";

            if (!Directory.Exists(pageFolder))
            {
                Directory.CreateDirectory(pageFolder);
            }

            // ---- Images -------------

            // download all images
            // Download all files on the current page (e.g., images, stylesheets)
            var imageLinks = htmlDoc.DocumentNode.SelectNodes("//img[@src]");

            if (imageLinks.Count > 0) {
                //create the images sub-folder
                var imagesSubFolder = $"{pageFolder}/images";
                if (!Directory.Exists(imagesSubFolder))
                {
                    Directory.CreateDirectory(imagesSubFolder);
                }

                Console.WriteLine($"{imageLinks.Count} images");
                foreach (var link in imageLinks)
                {
                    string fileUrl = new Uri(new Uri(baseUrl), link.GetAttributeValue("src", "")).AbsoluteUri;
                    Console.WriteLine($"image url: {fileUrl}");
                    await DownloadFileAndSave(fileUrl, imagesSubFolder);
                }
            }


            // find nxt page
            // Select the <a> tag under <ul> with class 'pager'
            HtmlNode nextPageATag = htmlDoc.DocumentNode.SelectSingleNode("//ul[@class='pager']/li/a"); // TODO
            string nextPageRelUrl = nextPageATag.GetAttributeValue("href", "");
            // string nextPageUrl = new Uri(new Uri(baseUrl), nextPageRelUrl).AbsoluteUri;

            // call travelse
            // await TraversePagesAsync(nextPageUrl);

            // del -- string fileUrl = new Uri(new Uri(url), link.GetAttributeValue("href", link.GetAttributeValue("src", ""))).AbsoluteUri;
        }
    }

    static string ExtractPartBeforeHtml(string relativeUrl)
    {
        // Use regular expression to match part before ".html"
        Regex regex = new Regex(@"^(.*?)\.html$");
        Match match = regex.Match(relativeUrl);

        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        else
        {
            // If no match, return the original URL
            return relativeUrl;
        }
    }

    private static void SavePageToDisk(string relativeUrl, HtmlDocument htmlDocument) { }

    static async Task ScrapePage(string url, string outputFolder)
    {
        using (HttpClient client = new())
        {
            HttpResponseMessage response = await client.GetAsync(url);
            string htmlContent = await response.Content.ReadAsStringAsync();

            HtmlDocument doc = new();
            doc.LoadHtml(htmlContent);

            // Download all files on the current page (e.g., images, stylesheets)
            var links = doc.DocumentNode.SelectNodes("//img[@src]");

            Console.WriteLine($"{links.Count} links");
            foreach (var link in links)
            {
                string fileUrl = new Uri(new Uri(url), link.GetAttributeValue("href", link.GetAttributeValue("src", ""))).AbsoluteUri;
                await DownloadFileAndSave(fileUrl, outputFolder);
            }

            // Download the HTML content
            string fileName = Path.Combine(outputFolder, Path.GetFileName(url) + ".html");
            File.WriteAllText(fileName, htmlContent);
            Console.WriteLine($"Downloaded: {url}");

            // Find and follow links to other pages
            foreach (var nextPageLink in doc.DocumentNode.SelectNodes("//a[@href]"))
            {
                string nextUrl = new Uri(new Uri(url), nextPageLink.GetAttributeValue("href", "")).AbsoluteUri;
                await ScrapePage(nextUrl, outputFolder);
            }
        }
    }

    static async Task DownloadFileAndSave(string url, string outputFolder)
    {
        using HttpClient client = new();
        HttpResponseMessage response = await client.GetAsync(url);
        string fileName = Path.Combine(outputFolder, Path.GetFileName(new Uri(url).LocalPath));
        await File.WriteAllBytesAsync(fileName, await response.Content.ReadAsByteArrayAsync());
        Console.WriteLine($"Downloaded: {url}");
    }
}
