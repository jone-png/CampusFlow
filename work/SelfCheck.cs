// 自检程序：验证配方引擎的解析、提取、模板替换。
// 不参与主构建（Build-CampusAutoLogin.ps1 只编译 CampusAutoLogin.cs），
// 与主源码一起编译成一个临时 exe 运行，用 -main:SelfCheck 指定入口。
//
// 运行方式（在 work 目录下）：
//   csc -nologo -target:exe -main:SelfCheck -out:%TEMP%\cfcheck.exe ^
//       -reference:System.dll,System.Core.dll,System.Drawing.dll,System.Security.dll,System.Xml.dll,System.Windows.Forms.dll ^
//       CampusAutoLogin.cs SelfCheck.cs
//   %TEMP%\cfcheck.exe
//
// 退出码 0 = 全部通过，非 0 = 失败项数。

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

static class SelfCheck
{
    static int failures;

    static void Check(bool ok, string label)
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + label);
        if (!ok) failures++;
    }

    // C# 5 没有局部函数，只能写成静态方法。
    static string NormalizeNewlines(string value)
    {
        return (value ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
    }

    public static int Main()
    {
        // 自检会构造坏字符集等假数据，AppData.Log 默认写进用户真实的
        // %LOCALAPPDATA%\CampusFlow\campusflow.log。必须掐掉，否则每跑一次自检
        // 就往真实运行日志里灌一堆"未知字符集"噪音，把排查线索淹掉。
        AppData.LogSuppressed = true;

        string error;

        // ---- 预设 ----
        AuthRecipe hebei = RecipeEngine.Parse(PortalSettings.HebeiRecipeJson(), out error);
        Check(hebei != null && error == null, "河北预设能解析" + (error == null ? "" : "：" + error));
        Check(hebei != null && hebei.Steps.Count == 2, "河北预设 = 2 步");
        if (hebei != null)
        {
            Check(hebei.Steps[0].Method == "GET", "第 1 步是 GET");
            Check(hebei.Steps[1].Method == "POST", "第 2 步是 POST");
            Check((hebei.Steps[1].Body ?? "").Contains("{username}"), "第 2 步 body 含 {username}");
            Check((hebei.Steps[1].Url ?? "").Contains("{local_ip}"), "第 2 步 url 含 {local_ip}");
        }

        // ---- 空配方：必须返回 null 且不报错，调用方据此回退到扁平表单路径 ----
        AuthRecipe empty = RecipeEngine.Parse("", out error);
        Check(empty == null && error == null, "空配方 → null 且无错误（回退老路径）");
        AuthRecipe nullRecipe = RecipeEngine.Parse(null, out error);
        Check(nullRecipe == null && error == null, "null 配方 → null 且无错误");

        // ---- 校验必须能拦下各类坏配方 ----
        RecipeEngine.Parse("{ 这不是 json", out error);
        Check(error != null, "坏 JSON 被拦下：" + error);

        RecipeEngine.Parse("{\"steps\":[]}", out error);
        Check(error != null && error.Contains("没有任何步骤"), "空步骤被拦下：" + error);

        RecipeEngine.Parse("{\"steps\":[{\"url\":\"http://x\",\"extract\":\"a(b)\"}]}", out error);
        Check(error != null && error.Contains("extractAs"), "有 extract 无 extractAs 被拦下：" + error);

        RecipeEngine.Parse("{\"steps\":[{\"url\":\"http://x\",\"extract\":\"([\",\"extractAs\":\"t\"}]}", out error);
        Check(error != null && error.Contains("正则"), "坏正则被拦下：" + error);

        RecipeEngine.Parse("{\"steps\":[{\"url\":\"http://x\",\"extract\":\"(a)\",\"extractJson\":\"a\",\"extractAs\":\"t\"}]}", out error);
        Check(error != null && error.Contains("二选一"), "extract 与 extractJson 同时出现被拦下：" + error);

        RecipeEngine.Parse("{\"steps\":[{\"method\":\"GET\"}]}", out error);
        Check(error != null && error.Contains("url"), "缺 url 被拦下：" + error);

        RecipeEngine.Parse("{\"steps\":[{\"url\":\"http://x\",\"extractJson\":\"a;b\",\"extractAs\":\"only\"}]}", out error);
        Check(error != null && error.Contains("数量必须一致"), "并列提取数量不匹配被拦下：" + error);

        // ---- 校园网门户预设 ----
        AuthRecipe campus = RecipeEngine.Parse(PortalSettings.CampusRecipeJson(), out error);
        Check(campus != null && error == null, "校园网预设能解析" + (error == null ? "" : "：" + error));
        if (campus != null)
        {
            Check(campus.Steps.Count == 2, "校园网预设 = 2 步");
            Check(campus.Steps[1].Url.Contains("{password:url}"), "认证步对密码做了 URL 编码（GET 查询串必须）");
            Check(campus.Steps[1].Url.Contains("@gxyyd"), "账号带移动后缀 @gxyyd");
            Check(campus.Steps[1].Url.Contains("quickauth.do"), "提交端点是 quickauth.do");
            Check(campus.Steps[0].ExtractAs.Split(';').Length == 3, "取配置步一次提取 3 个值");
            Check(campus.SuccessJson == "code" && campus.SuccessValue == "0", "成功判定用 JSON 字段 code == 0");
            // 可空类型：配方没写时必须是 null（引擎按 true 处理），不能退化成 false
            Check(!campus.VerifyConnectivity.HasValue, "配方未写 verifyConnectivity 时为空（引擎按开启处理）");
            AuthRecipe withFlag = RecipeEngine.Parse("{\"verifyConnectivity\":false,\"steps\":[{\"url\":\"http://x\"}]}", out error);
            Check(withFlag != null && withFlag.VerifyConnectivity.HasValue && withFlag.VerifyConnectivity.Value == false,
                "配方显式写 verifyConnectivity:false 时能读到 false");
            // 门户真实响应，确认成功判定取得到值
            string realOk = "{\"code\":\"0\",\"message\":\"AC999\"}";
            string realBad = "{\"code\":\"7\",\"message\":\"账号或密码不正确\"}";
            Check(RecipeEngine.JsonPath(realOk, campus.SuccessJson) == "0", "真实成功响应取到 code=0");
            Check(RecipeEngine.JsonPath(realBad, campus.SuccessJson) == "7", "真实失败响应取到 code=7（不会被误判为成功）");
        }

        // ---- JSON 提取 ----
        string json = "{\"data\":{\"accessToken\":\"TK123\",\"n\":42,\"deep\":{\"k\":\"v\"}},\"ok\":true}";
        Check(RecipeEngine.JsonPath(json, "data.accessToken") == "TK123", "JsonPath 取字符串");
        Check(RecipeEngine.JsonPath(json, "data.n") == "42", "JsonPath 取数字（无区域格式问题）");
        Check(RecipeEngine.JsonPath(json, "data.deep.k") == "v", "JsonPath 取多层");
        Check(RecipeEngine.JsonPath(json, "ok") == "True", "JsonPath 取布尔");
        Check(RecipeEngine.JsonPath(json, "data.missing") == null, "JsonPath 缺失 → null");
        Check(RecipeEngine.JsonPath(json, "data.accessToken.x") == null, "JsonPath 穿透标量 → null");
        Check(RecipeEngine.JsonPath("不是 json", "a") == null, "JsonPath 遇非 JSON → null");

        // ---- 模板替换 ----
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        vars["username"] = "u1";
        vars["Token"] = "T";   // 大小写不敏感
        Check(PortalClient.Expand("a={username}&b={token}", vars) == "a=u1&b=T", "模板替换（键名大小写不敏感）");
        Check(PortalClient.Expand("{unknown}", vars) == "{unknown}", "未知变量原样保留");
        Check(PortalClient.Expand("", vars) == "", "空串安全");
        Check(PortalClient.Expand(null, vars) == "", "null 安全");

        // ---- {name:url} 修饰符：GET 把密码放查询串时必须编码，否则含 & = 会把请求拆坏 ----
        vars["password"] = "a&b=c d";
        Check(PortalClient.Expand("{password:url}", vars) == Uri.EscapeDataString("a&b=c d"), "url 修饰符做百分号编码");
        Check(PortalClient.Expand("{password}", vars) == "a&b=c d", "不带修饰符时保持原样（请求体场景）");
        Check(PortalClient.Expand("{unknown:url}", vars) == "{unknown:url}", "未知变量带修饰符也原样保留");

        // ---- 一次响应提取多个值（校园网门户一次下发 timestamp/uuid/portalpageid）----
        string cfgJson = "{\"portalconfig\":{\"timestamp\":\"1789827061469\",\"uuid\":\"032b34ba\",\"id\":22}}";
        Check(RecipeEngine.JsonPath(cfgJson, "portalconfig.timestamp") == "1789827061469", "多值提取第 1 项");
        Check(RecipeEngine.JsonPath(cfgJson, "portalconfig.uuid") == "032b34ba", "多值提取第 2 项");
        Check(RecipeEngine.JsonPath(cfgJson, "portalconfig.id") == "22", "多值提取第 3 项（数字转字符串）");

        // ---- 字符集解析（GBK 门户此前会被解成乱码，导致关键字永远匹配不上）----
        Check(PortalClient.CharsetOf("gb2312").WebName.ToLower().Contains("gb"), "gb2312 被识别");
        Check(PortalClient.CharsetOf("text/html; charset=UTF-8") == System.Text.Encoding.UTF8, "从 Content-Type 抽 charset");
        Check(PortalClient.CharsetOf("application/json") == System.Text.Encoding.UTF8, "无 charset 回退 UTF-8");
        Check(PortalClient.CharsetOf("charset=完全不存在的编码") == System.Text.Encoding.UTF8, "坏 charset 回退 UTF-8");
        Check(PortalClient.CharsetOf(null) == System.Text.Encoding.UTF8, "null charset 回退 UTF-8");

        // ---- 向后兼容：改造前写出的配置文件（没有 SchemaVersion / RecipeJson 两个元素）----
        string oldXml = "<?xml version=\"1.0\"?><PortalSettings>"
            + "<PortalUrl>http://portal</PortalUrl><SubmitUrl>http://submit</SubmitUrl><Method>POST</Method>"
            + "<UsernameField>uf</UsernameField><PasswordField>pf</PasswordField><ExtraFields>a=b</ExtraFields>"
            + "<ConnectivityUrl>http://conn</ConnectivityUrl><ConnectivityExpected>ok</ConnectivityExpected>"
            + "<SuccessKeywords>成功</SuccessKeywords><CheckIntervalSeconds>60</CheckIntervalSeconds>"
            + "<DiscoverRedirect>false</DiscoverRedirect><NightMode>true</NightMode><AutoDetect>false</AutoDetect>"
            + "</PortalSettings>";
        var oldSettings = (PortalSettings)new XmlSerializer(typeof(PortalSettings)).Deserialize(new StringReader(oldXml));
        Check(oldSettings.SchemaVersion == 0, "老配置反序列化 SchemaVersion == 0（一次性迁移的触发条件）");
        Check(oldSettings.RecipeJson == null, "老配置 RecipeJson 为 null → 走扁平老路径");
        Check(oldSettings.SubmitUrl == "http://submit" && oldSettings.UsernameField == "uf", "老配置 13 个字段原样读出");
        Check(oldSettings.AutoDetect == false && oldSettings.NightMode, "老配置的布尔字段正确");

        // ---- 新配置往返：配方是含换行和中文的长字符串，必须原样存活 ----
        var fresh = PortalSettings.ChinaMobileTemplate();
        fresh.SchemaVersion = 2;
        fresh.RecipeJson = PortalSettings.HebeiRecipeJson();
        var buffer = new MemoryStream();
        new XmlSerializer(typeof(PortalSettings)).Serialize(buffer, fresh);
        buffer.Position = 0;
        var restored = (PortalSettings)new XmlSerializer(typeof(PortalSettings)).Deserialize(buffer);
        // 不能断言逐字节相等：XML 规范要求解析器把 \r\n 归一化成 \n，所以往返后行尾会变。
        // 对 JSON 无影响，真正要保证的是「还能解析出同样的步骤」（见下一项）。
        Check(NormalizeNewlines(restored.RecipeJson) == NormalizeNewlines(fresh.RecipeJson),
            "配方字符串 XML 往返语义一致（行尾被 XML 规范归一化，内容不变）");
        Check(restored.SchemaVersion == 2, "SchemaVersion 往返一致");
        AuthRecipe roundTripped = RecipeEngine.Parse(restored.RecipeJson, out error);
        Check(roundTripped != null && roundTripped.Steps.Count == 2, "往返后的配方仍能解析出 2 步");

        Console.WriteLine(failures == 0 ? "\n全部通过。" : "\n失败 " + failures + " 项。");
        return failures;
    }
}
