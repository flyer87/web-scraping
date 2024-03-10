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

        using (HttpClient client = new())
        {
            string htmlContent = await GetHtmlContent(fullUrl, client);

            HtmlDocument htmlDoc = new();
            // Load the HTML content into an HtmlDocument
            htmlDoc.LoadHtml(htmlContent);

            // create the page folder
            var pageFolder = GetFolderNameFromUrl(relativeUrl);
            if (!Directory.Exists(pageFolder))
            {
                Directory.CreateDirectory(pageFolder);
            }

            // ---- Images -------------
            await DownloadAllImages(htmlDoc, pageFolder);

            // get next page url
            string nextPageRelUrl = BuildNextPageUrl(htmlDoc);

            await TraversePagesAsync(nextPageRelUrl);
        }
    }

    private static async Task DownloadAllImages(HtmlDocument htmlDoc, string pageFolder)
    {
        // get all image links
        var imageLinks = htmlDoc.DocumentNode.SelectNodes("//img[@src]");

        if (imageLinks.Count == 0) return;

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

    private static string BuildNextPageUrl(HtmlDocument htmlDoc)
    {
        // Select the <a> tag under <ul> with class 'pager'
        HtmlNode nextPageATag = htmlDoc.DocumentNode.SelectSingleNode("//ul[@class='pager']/li[@class='next']/a");
        if (nextPageATag == null) { return ""; }

        string nextPageHRef = nextPageATag.GetAttributeValue("href", "");
        var nextPageRelUrl = AddCatalogueToRelUrl(nextPageHRef);

        return nextPageRelUrl;
    }

    private static string AddCatalogueToRelUrl(string relUrl)
    {
        if (relUrl.Contains("catalogue/"))
        {
            return relUrl;
        }

        return "catalogue/" + relUrl;
    }

    private static async Task<string> GetHtmlContent(string fullUrl, HttpClient client)
    {
        string htmlContent = "";

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

    static string GetFolderNameFromUrl(string url)
    {
        // Find the index of ".html" in the URL
        int endIndex = url.IndexOf(".html", StringComparison.OrdinalIgnoreCase);

        // If ".html" is found, return the substring before it; otherwise, return the original URL
        return endIndex != -1 ? url[..endIndex] : url;
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