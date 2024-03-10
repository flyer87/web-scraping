using System;
using System.Collections;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using static System.Net.Mime.MediaTypeNames;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();
    private static readonly string baseUrl = "https://books.toscrape.com/";
    private static readonly byte MAX_PAGES = 5;
    private static int currentPage = 0;

    static async Task Main()
    {
        await TraversePagesAsync("index.html");
        Console.WriteLine("Scraping completed!");
    }


    private static async Task TraversePagesAsync(string relativeUrl)
    {
        //currentPage++;
        //if (currentPage > MAX_PAGES)
        //{
        //    return;
        //}

        if (string.IsNullOrEmpty(relativeUrl))
        {
            return;
        }

        string fullUrl = baseUrl + relativeUrl;

        Console.WriteLine($"Processing: {fullUrl}");

        string htmlContent = "";
        using (HttpClient client = new())
        {
            htmlContent = await GetHtmlContent(fullUrl, htmlContent, client);

            HtmlDocument htmlDoc = new();

            // Load the HTML content into an HtmlDocument
            htmlDoc.LoadHtml(htmlContent);

            // create the page folder
            var pageFolder = ExtractPartFromUrl(relativeUrl);

            if (!Directory.Exists(pageFolder))
            {
                Directory.CreateDirectory(pageFolder);
            }

            // ---- Images -------------

            // download all images
            var imageLinks = htmlDoc.DocumentNode.SelectNodes("//img[@src]");

            if (imageLinks.Count > 0)
            {
                //create the images sub-folder
                var imagesSubFolder = $"{pageFolder}/images";
                if (!Directory.Exists(imagesSubFolder))
                {
                    Directory.CreateDirectory(imagesSubFolder);
                }

                Console.WriteLine($"{imageLinks.Count} images");


                var downloadTasks = imageLinks.Select(link =>
                {
                    string fileUrl = new Uri(new Uri(baseUrl), link.GetAttributeValue("src", "")).AbsoluteUri;
                    return DownloadFileAndSave(fileUrl, imagesSubFolder);
                });

                await Task.WhenAll(downloadTasks);
            }


            // find nxt page
            // Select the <a> tag under <ul> with class 'pager'
            HtmlNode nextPageATag = htmlDoc.DocumentNode.SelectSingleNode("//ul[@class='pager']/li[@class='next']/a"); // TODO
            if (nextPageATag == null) { return; }
            string nextPageHRef = nextPageATag.GetAttributeValue("href", "");

            var nextPageRelUrl = AddCatalogueToRelUrl(nextPageHRef);
            await TraversePagesAsync(nextPageRelUrl);
        }
    }

    private static string AddCatalogueToRelUrl(string relUrl)
    {
        if (relUrl.Contains("catalogue/"))
        {
            return relUrl;
        }

        return "catalogue/" + relUrl;
    }

    private static async Task<string> GetHtmlContent(string fullUrl, string htmlContent, HttpClient client)
    {
        try
        {
            HttpResponseMessage response = await client.GetAsync(fullUrl);
            if (response.IsSuccessStatusCode)
            {
                htmlContent = await response.Content.ReadAsStringAsync();
                // Process the HTML content
            }
            else
            {
                // Handle unsuccessful response
                Console.WriteLine($"Failed to fetch content. Status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            // Log or handle the exception
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

        return htmlContent;
    }

    static string ExtractPartFromUrl(string url)
    {
        // Use regular expression to match part before ".html"
        Regex regex = new Regex(@"^(.*?)\.html$");
        Match match = regex.Match(url);

        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        else
        {
            // If no match, return the original URL
            return url;
        }
    }

    //static string ExtractPartBeforeHtml(string relativeUrl)
    //{
    //    // Use regular expression to match part before ".html"
    //    // Regex regex = new Regex(@"^(.*?)\.html$");
    //    // string pattern = @"^(.*?\.html)";
    //    string pattern = @$"{baseUrl}(.*?)$";
    //    Match match = Regex.Match(relativeUrl, pattern);

    //    if (match.Success)
    //    {
    //        return match.Groups[1].Value;
    //    }
    //    else
    //    {
    //        // If no match, return the original URL
    //        return relativeUrl;
    //    }
    //}

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
        // Console.WriteLine($"Downloaded: {url}");
    }
}