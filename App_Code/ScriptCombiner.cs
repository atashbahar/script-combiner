using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web;

public class ScriptCombiner : IHttpHandler
{
    private readonly static TimeSpan CACHE_DURATION = TimeSpan.FromDays(30);
    private HttpContext context;

    public void ProcessRequest(HttpContext context)
    {
        this.context = context;
        HttpRequest request = context.Request;        

        // Read setName, version from query string
        string setName = request["s"] ?? string.Empty;
        string version = request["v"] ?? string.Empty;

        // Decide if browser supports compressed response
        bool isCompressed = this.CanGZip(context.Request);

        // If the set has already been cached, write the response directly from
        // cache. Otherwise generate the response and cache it
        if (!this.WriteFromCache(setName, version, isCompressed))
        {
            using (MemoryStream memoryStream = new MemoryStream(8092))
            {
                // Decide regular stream or gzip stream based on whether the response can be compressed or not
                //using (Stream writer = isCompressed ?  (Stream)(new GZipStream(memoryStream, CompressionMode.Compress)) : memoryStream)
                using (Stream writer = isCompressed ? (Stream)(new ICSharpCode.SharpZipLib.GZip.GZipOutputStream(memoryStream)) : memoryStream)                
                {
                    // Read the files into one big string
                    StringBuilder allScripts = new StringBuilder();
                    foreach (string fileName in GetScriptFileNames(setName))
                        allScripts.Append(File.ReadAllText(context.Server.MapPath(fileName)));

                    // Minify the combined script files and remove comments and white spaces
                    var minifier = new JavaScriptMinifier();
                    string minified = minifier.Minify(allScripts.ToString());

                    // Send minfied string to output stream
                    byte[] bts = Encoding.UTF8.GetBytes(minified);
                    writer.Write(bts, 0, bts.Length);
                }

                // Cache the combined response so that it can be directly written
                // in subsequent calls 
                byte[] responseBytes = memoryStream.ToArray();
                context.Cache.Insert(GetCacheKey(setName, version, isCompressed),
                    responseBytes, null, System.Web.Caching.Cache.NoAbsoluteExpiration,
                    CACHE_DURATION);

                // Generate the response
                this.WriteBytes(responseBytes, isCompressed);
            }
        }
    }
    private bool WriteFromCache(string setName, string version, bool isCompressed)
    {
        byte[] responseBytes = context.Cache[GetCacheKey(setName, version, isCompressed)] as byte[];

        if (responseBytes == null || responseBytes.Length == 0)
            return false;

        this.WriteBytes(responseBytes, isCompressed);
        return true;
    }

    private void WriteBytes(byte[] bytes, bool isCompressed)
    {
        HttpResponse response = context.Response;

        response.AppendHeader("Content-Length", bytes.Length.ToString());
        response.ContentType = "application/x-javascript";
        if (isCompressed)
            response.AppendHeader("Content-Encoding", "gzip");
        else
            response.AppendHeader("Content-Encoding", "utf-8");

        context.Response.Cache.SetCacheability(HttpCacheability.Public);
        context.Response.Cache.SetExpires(DateTime.Now.Add(CACHE_DURATION));
        context.Response.Cache.SetMaxAge(CACHE_DURATION);

        response.ContentEncoding = Encoding.Unicode;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Flush();
    }

    private bool CanGZip(HttpRequest request)
    {
        string acceptEncoding = request.Headers["Accept-Encoding"];
        if (!string.IsNullOrEmpty(acceptEncoding) &&
             (acceptEncoding.Contains("gzip") || acceptEncoding.Contains("deflate")))
            return true;
        return false;
    }

    private string GetCacheKey(string setName, string version, bool isCompressed)
    {
        return "HttpCombiner." + setName + "." + version + "." + isCompressed;
    }

    public bool IsReusable
    {
        get { return true; }
    }

    // private helper method that return an array of file names inside the text file stored in App_Data folder
    private static string[] GetScriptFileNames(string setName)
    {
        var scripts = new System.Collections.Generic.List<string>();
        string setPath = HttpContext.Current.Server.MapPath(String.Format("~/App_Data/{0}.txt", setName));
        using (var setDefinition = File.OpenText(setPath))
        {
            string fileName = null;
            while (setDefinition.Peek() >= 0)
            {
                fileName = setDefinition.ReadLine();
                if (!String.IsNullOrEmpty(fileName))
                    scripts.Add(fileName);
            }
        }
        return scripts.ToArray();

    }

    public static string GetScriptTags(string setName, int version)
    {
        string result = null;
#if (DEBUG)            
            foreach (string fileName in GetScriptFileNames(setName))
            {
                result += String.Format("\n<script type=\"text/javascript\" src=\"{0}?v={1}\"></script>", VirtualPathUtility.ToAbsolute(fileName), version);
            }
#else
        result += String.Format("<script type=\"text/javascript\" src=\"ScriptCombiner.axd?s={0}&v={1}\"></script>", setName, version);
#endif
        return result;
    }
}
