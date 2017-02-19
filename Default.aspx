<%@ Page Language="C#" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<script runat="server">

</script>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Combine, minify and compress JavaScript files </title>    
    <%= ScriptCombiner.GetScriptTags("Site_Scripts", 1) %>
</head>
<body>
    <form id="form1" runat="server">
    <div>
        <h1>Combine, minify and compress JavaScript files</h1>
        <%= Server.HtmlEncode(ScriptCombiner.GetScriptTags("Site_Scripts", 1)) %>
    </div>
    
    </form>
</body>
</html>
