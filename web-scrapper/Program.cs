﻿using HtmlAgilityPack;

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

    private static async Task TraversePagesAsync(string relativeUrl)
    {
        if (string.IsNullOrEmpty(relativeUrl))
        {
            return;
        }

        string fullUrl = baseUrl + relativeUrl;
        Console.WriteLine($"Processing: {fullUrl}");

        using (HttpClient client = new())
        {
            string htmlContent = await GetHtmlContent(fullUrl, client);

            // Load the HTML content into an HtmlDocument
            HtmlDocument htmlDoc = new();
            htmlDoc.LoadHtml(htmlContent);

            // create the page folder
            var pageFolder = GetFolderNameFromUrl(relativeUrl);
            if (!Directory.Exists(pageFolder))
            {
                Directory.CreateDirectory(pageFolder);
            }

            // save html file
            await DownloadFileAndSave(fullUrl, pageFolder);

            // save images
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
        var nextPageRelUrl = AddCatalogueToLink(nextPageHRef);

        return nextPageRelUrl;
    }

    private static string AddCatalogueToLink(string relUrl)
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
    }
}