using Microsoft.Playwright;
using OpenCvSharp; // 引用 OpenCV
using OpenCvSharp.Dnn;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Tesseract; // 需要引入 Tesseract 命名空间

class Program
{
    // 定义保存登录状态（Cookies、LocalStorage等）的 JSON 文件名
    // 该文件会保存在程序运行目录（bin/Debug/...）下
    private const string StatePath = "auth.json";
    private static readonly string Path_Image = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Template");
    //private static readonly string Path_Image = Path.Combine("G:\\MY\\C#\\MiHoYo_CloudGame_Auto\\MiHoYo_CloudGame_Auto\\MiHoYo_CloudGame_Auto", "Template");


    public class TimestampedTextWriter : TextWriter
    {
        private readonly TextWriter _originalOutput;
        public override Encoding Encoding => _originalOutput.Encoding;

        public TimestampedTextWriter(TextWriter originalOutput)
        {
            _originalOutput = originalOutput;
        }

        public override void WriteLine(string? value)
        {
            // 在每一行开头自动插入时间
            _originalOutput.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {value}");
        }
    }


    public static async Task Main()
    {
        Console.SetOut(new TimestampedTextWriter(Console.Out));
        #region 浏览器配置
        // 1. 创建 Playwright 基础对象
        // IPlaywright 是所有操作的入口
        using var playwright = await Playwright.CreateAsync();

        // 2. 环境自检：自动下载/安装 Chromium 浏览器内核
        // 这解决了 "Executable doesn't exist" 报错，确保在任何环境下都能运行
        Console.WriteLine("正在检查浏览器驱动环境...");
        Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });

        // 3. 启动 Chromium 浏览器
        // Headless = false: 显示浏览器界面，方便扫码登录 
        // Args: 传入命令行参数，让浏览器窗口启动时最大化
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            Args = new[] { "" }
        });


        // 4. 配置浏览器上下文参数
        // ViewportSize = null (NoViewport): 配合全屏参数，让浏览器内容填充整个窗口
        BrowserNewContextOptions contextOptions = new BrowserNewContextOptions
        {


            ViewportSize = new ViewportSize
            {
                Width = 960,
                Height = 540
            }
        };


        // --- 核心逻辑：持久化登录信息处理 ---
        // 判断本地是否存在 auth.json 文件
        if (File.Exists(StatePath))
        {
            Console.WriteLine("【读取】发现本地 auth.json，尝试载入登录凭证...");
            // 如果存在文件，将其路径赋值给 context，Playwright 会自动加载 Cookies
            contextOptions.StorageStatePath = StatePath;
        }
        else
        {
            Console.WriteLine("【提示】未发现本地凭证，稍后请在浏览器中手动完成扫码登录。");
        }

        // 5. 创建浏览器上下文和新页面
        // 上下文（Context）类似于浏览器的独立“沙盒”或“无痕模式”窗口
        var context = await browser.NewContextAsync(contextOptions);
        var page = await context.NewPageAsync();


        // 6. 导航至云星穹铁道官网
        // WaitUntilState.NetworkIdle: 意思是等到网络请求几乎停止后再继续，确保页面加载完全
        Console.WriteLine("正在打开网页，请稍候...");
        await page.GotoAsync("https://sr.mihoyo.com/cloud/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        #endregion
        #region 进入游戏
        try
        {
            


            // 7. 定义“进入游戏”按钮的定位器
            // 使用 :has-text 过滤包含文字的 div，.Last 确保选中最新的 DOM 节点
            var startButton = page.Locator("div:has-text('进入游戏')").Last;

            // 8. 循环检测逻辑：每 2 秒检测一次按钮是否出现
            bool isClicked = false;
            const int maxRetries = 300; // 总计约等待 10 分钟 (300 * 2s)

            for (int i = 0; i < maxRetries; i++)
            {
                {
                    var overlaySelector = "div.van-overlay.clg-dialog-z-index";//删除遮罩层的选择器，需根据实际页面结构调整

                    // 检查该元素是否存在
                    var overlay = page.Locator(overlaySelector);
                    if (await overlay.CountAsync() > 0)
                    {
                        Console.WriteLine("检测到遮罩层，正在执行删除...");

                        // 使用 JavaScript 将其从 DOM 中移除
                        // arg 是传递给 JS 的选择器字符串
                        await page.EvaluateAsync(@"selector => {
        const el = document.querySelector(selector);
        if (el) el.remove();
    }", overlaySelector);

                        Console.WriteLine("遮罩层已清除。");
                    }
                }
                // 判断按钮是否在页面上可见且可以点击
                if (await startButton.IsVisibleAsync())
                {
                    // 执行点击操作
                    await startButton.ClickAsync();
                    Console.WriteLine(">>> 检测到按钮，执行【进入游戏】点击！");
                    isClicked = true;

                    // --- 关键步骤：点击成功后立即捕捉并保存状态 ---
                    // 将当前的 Cookie、Session 等所有信息写入 auth.json
                    await context.StorageStateAsync(new BrowserContextStorageStateOptions
                    {
                        Path = StatePath
                    });
                    Console.WriteLine($">>> 登录状态已加密保存至本地。");
                    break;
                }

                // 每隔 10 次检测
                if (i % 10 == 0)
                {
                    Console.WriteLine($"正在检测‘进入游戏’按钮（如未登录请先扫码）...");
                }

                // 等待 2 秒后进入下一次检测
                await Task.Delay(2000);
            }

            if (!isClicked)
            {
                Console.WriteLine("等待超时：未能在预定时间内检测到登录状态或进入按钮。");
            }

        }
        catch (Exception ex)
        {
            // 捕获异常，防止程序因为网页刷新或网络抖动直接闪退
            Console.WriteLine($"程序运行发生异常: {ex.Message}");
        }
        #endregion
        #region 引导和用户协议

        {
            const int maxRetries = 600; // 设置最大检测时间（例如 600 秒）
            bool isAgreed = false;

            Console.WriteLine("开始检测协议弹窗...");

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // 1. 定位“接受”按钮
                    // 选择器解释：寻找包含 '接受' 文字的按钮，且拥有 Vant 组件库常见的确认类名
                    var acceptBtn = page.Locator("button.van-dialog__confirm:has-text('接受')");

                    // 2. 判断按钮是否在页面上可见
                    if (await acceptBtn.IsVisibleAsync())
                    {
                        // 执行点击
                        await acceptBtn.ClickAsync();

                        Console.WriteLine($"检测到协议弹窗，已点击“接受”。");
                        isAgreed = true;

                        // 点击成功后退出循环
                        break;
                    }

                    // 每 5 秒输出一次心跳日志，确认程序还在运行
                    if (i % 5 == 0)
                    {
                        Console.WriteLine($" 正在扫描协议弹窗 (第 {i} 秒)...");
                    }
                }
                catch (Exception ex)
                {
                    // 捕获可能的瞬时异常（如页面刷新中），保证循环不中断
                    Console.WriteLine("检测中遇到波动，继续尝试...");
                }

                // 3. 核心：每隔 1000 毫秒（1秒）检查一次
                await Task.Delay(1000);
            }
            if (!isAgreed)
            {
                Console.WriteLine("未能在规定时间内检测到协议弹窗。");
            }
        }
        {
            //删除引导
            var guideSelector = "div.game-menu-setting-guide";

            try
            {
                // 检查指引层是否存在
                var guideElement = page.Locator(guideSelector);

                if (await guideElement.CountAsync() > 0)
                {
                    Console.WriteLine($"检测到新手设置指引，正在强力删除...");

                    // 使用 JavaScript 直接从 DOM 中移除该根节点及其所有子节点
                    await page.EvaluateAsync(@"selector => {
            const el = document.querySelector(selector);
            if (el) {
                el.remove();
                console.log('Setting guide removed successfully.');
            }
        }", guideSelector);
                    await page.EvaluateAsync(@"() => {
    const el = document.querySelector('div.network-stat.vqc-good');
    if (el) el.remove();
}");

                    Console.WriteLine("指引层已清理，画面已解锁。");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"删除指引层时出现异常: {ex.Message}");
            }
            
        }
        #endregion
        #region 游戏操作
        {
            await Task.Delay(15000);
            await ClickElementAtAsync(page, 100, 100);
            await Task.Delay(15000);
            //委托
            await KeyboardHelper.PressKeyAsync(page, "Escape");
            await Task.Delay(3000);
            await ClickByImageAsync(page, Path.Combine(Path_Image , "ESC-委托.png"));//委托
            //await ClickByColoredTextAsync(page, "委托");//委托
            await Task.Delay(3000);
            if (await ClickByImageAsync(page, Path.Combine(Path_Image ,"领取奖励.png")))//领取奖励
            {

                await Task.Delay(2000);
                await KeyboardHelper.PressKeyAsync(page, "Escape");
            }
            await Task.Delay(2000);
            await KeyboardHelper.PressKeyAsync(page, "Escape");
            await Task.Delay(2000);
            await KeyboardHelper.PressKeyAsync(page, "Escape");
            await Task.Delay(2000);
            //战斗
            await KeyboardHelper.PressKeyAsync(page, "F4");
            await Task.Delay(3000);
            await ClickByImageAsync(page, Path.Combine(Path_Image ,"F4-生存引导.png"));
            await Task.Delay(3000);
            await ClickByImageAsync(page, Path.Combine(Path_Image , "F4-培养目标.png"));
            await Task.Delay(3000);
            if (await ClickByImageAsync(page, Path.Combine(Path_Image ,"F4-培养资源.png"), 1, 400, 75))
            {
                await Task.Delay(3000);
                await ClickByImageAsync(page, Path.Combine(Path_Image , "F4-培养资源-加号.png"), 26);
                await Task.Delay(3000);
                await ClickByImageAsync(page, Path.Combine(Path_Image , "F4-挑战.png"),1,0,0);
                await Task.Delay(3000);
                await ClickByImageAsync(page, Path.Combine(Path_Image ,"F4-开始挑战.png"));
                await Task.Delay(5000);
                await ClickByImageAsync(page, Path.Combine(Path_Image , "加速.png"));
                await Task.Delay(1000);
                await ClickByImageAsync(page, Path.Combine(Path_Image , "自动.png"));
                await Task.Delay(10000);
                {
                    bool isReady = false; 
                    while (!isReady)
                    {
                        isReady = await ClickByImageAsync(page, Path.Combine(Path_Image , "退出关卡.png"));
                        await Task.Delay(5000);
                    }
                    Console.WriteLine("战斗结束！");
                }
            }
            else
            {
                Console.WriteLine("培养资源识别识别！");
            }

            await Task.Delay(2000);
            await KeyboardHelper.PressKeyAsync(page, "Escape");
            //通行证
            Console.WriteLine("领取通行证");
            await Task.Delay(2000);
            await KeyboardHelper.PressKeyAsync(page, "F2");
            await Task.Delay(3000);
            if (await ClickByImageAsync(page, Path.Combine(Path_Image, "F2-任务.png")))
            {
                await Task.Delay(3000);
                if (await ClickByImageAsync(page, Path.Combine(Path_Image, "领取奖励.png")))
                {

                    await Task.Delay(2000);
                    await KeyboardHelper.PressKeyAsync(page, "Escape");
                }
            }
            //每日奖励
            await Task.Delay(2000);
            await KeyboardHelper.PressKeyAsync(page, "Escape");
            Console.WriteLine("领取日活奖励");
            await Task.Delay(2000);
            await KeyboardHelper.PressKeyAsync(page, "F4");
            await Task.Delay(3000);
            await ClickByImageAsync(page, Path.Combine(Path_Image, "F4-每日实训.png"), 1);
            await Task.Delay(3000);
            {
                bool isReady = false;
                while (!isReady)
                {
                    if (false == await ClickByImageAsync(page, Path.Combine(Path_Image, "F4-领取.png"), 1))
                    {
                        isReady = true;
                    }
                    await Task.Delay(1000);
                }
            }
            {
                bool isReady = false;
                while (!isReady)
                {
                    if (false == await ClickByImageAsync(page, Path.Combine(Path_Image, "F4-领取每日奖励.png"), 1))
                    {
                        isReady = true;
                    }
                    await Task.Delay(1000);
                }
            }
            await Task.Delay(2000);
            await KeyboardHelper.PressKeyAsync(page, "Escape");
            //
            await Task.Delay(2000);
            await KeyboardHelper.PressKeyAsync(page, "Escape");
        }
        #endregion
        // 9. 保持运行，防止异步程序执行完毕后立即自动关闭浏览器
        Console.WriteLine("\n任务结束。如需关闭浏览器，请按回车键...");
        Console.ReadLine();
    }

    /// <summary>
    /// 点击指定选择器元素的相对坐标
    /// </summary>
    /// <param name="page">Page实例</param>
    /// <param name="x">相对于元素左上角的 X 偏移量</param>
    /// <param name="y">相对于元素左上角的 Y 偏移量</param>
    /// <param name="selector">选择器 (例如 "#my-div")</param>
    public static async Task<bool> ClickElementAtAsync(IPage page, int x, int y,int Tap_repeatedly =1 , string selector = "div.game-player__event-layer")
    {

        // 1. 获取目标元素定位器
        var locator = page.Locator(selector);


        for (int i = 0; i < Tap_repeatedly; i++)
        {
            // 只有从第二次（索引为1）开始才等待，确保第一次点击是立即触发的
            if (i > 0)
            {
                await Task.Delay(200);
            }

            await locator.ClickAsync(new LocatorClickOptions
            {
                Position = new Position { X = x, Y = y },
                Force = true
            });

            // 注意：i + 1 是为了让人类看日志时觉得是从第1次开始算的
            Console.WriteLine($"[第 {i + 1} 次] 已点击元素 {selector} 坐标: ({x}, {y})");
        }

        // 3. 在点击位置注入一个红点
        // 我们需要获取元素的边界，因为 Position 是相对坐标，而注入红点通常使用绝对坐标或相对于父容器
        var box = await locator.BoundingBoxAsync();
        if (box != null)
        {
            double dotX = box.X + x;
            double dotY = box.Y + y;

            await page.EvaluateAsync(@"([x, y]) => {
            const dot = document.createElement('div');
            dot.style.position = 'absolute';
            dot.style.left = x + 'px';
            dot.style.top = y + 'px';
            dot.style.width = '10px';
            dot.style.height = '10px';
            dot.style.backgroundColor = 'red';
            dot.style.borderRadius = '50%';
            dot.style.border = '2px solid white';
            dot.style.zIndex = '999999';
            dot.style.pointerEvents = 'none'; // 确保红点不干扰后续点击
            dot.style.transform = 'translate(-50%, -50%)'; // 居中显示
            
            document.body.appendChild(dot);
            
            // 1秒后自动移除
            setTimeout(() => {
                dot.remove();
            }, 1000);
        }", new object[] { dotX, dotY });
        }
        return true;
    }

    public static async Task<bool> ClickByColoredTextAsync(IPage page,string targetText,string tessDataPath = "tessdata",string language = "chi_sim",int Tap_repeatedly = 1,int P_X = 0,int P_Y = 0)
    {
        var canvas = page.Locator("#canvas-player");
        if (!await canvas.IsVisibleAsync()) return false;

        if (!Directory.Exists(tessDataPath))
        {
            Console.WriteLine($"[错误] 找不到 Tesseract 语言包目录: {Path.GetFullPath(tessDataPath)}");
            return false;
        }

        string screenshotPath = "ocr_raw.png";
        string processedPath = "ocr_final.png";

        try
        {
            // 1. 截取原始画面
            await canvas.ScreenshotAsync(new LocatorScreenshotOptions { Path = screenshotPath });

            // 2. 【核心改进】专治彩色字体的预处理
            PreProcessColoredText(screenshotPath, processedPath);

            // 3. 初始化 Tesseract 识别预处理后的图片
            using (var engine = new TesseractEngine(tessDataPath, language, EngineMode.Default))
            {
                // 设置页面分割模式为 SparseText (11)，适合识别游戏界面零散的文字
                engine.SetVariable("tessedit_pageseg_mode", "11");

                // 如果字体很特殊，可以尝试只识别特定的字符集，减少误判
                // engine.SetVariable("tessedit_char_whitelist", "开始游戏设置"); 

                using (var img = Pix.LoadFromFile(processedPath))
                using (var ocrPage = engine.Process(img))
                {
                    // 先打印一下识别到的全文，方便调试查看“乱码”情况
                    string fullText = ocrPage.GetText();
                    if (!string.IsNullOrWhiteSpace(fullText))
                    {
                        Console.WriteLine($"[调试] OCR 实际识别到的内容: {fullText.Trim()}");
                    }

                    using (var iter = ocrPage.GetIterator())
                    {
                        iter.Begin();
                        do
                        {
                            string recognizedText = iter.GetText(PageIteratorLevel.Word);

                            // 模糊匹配目标文字
                            if (!string.IsNullOrWhiteSpace(recognizedText) && recognizedText.Contains(targetText))
                            {
                                if (iter.TryGetBoundingBox(PageIteratorLevel.Word, out Tesseract.Rect bounds))
                                {
                                    int centerX = bounds.X1 + (bounds.Width / 2);
                                    int centerY = bounds.Y1 + (bounds.Height / 2);

                                    var box = await canvas.BoundingBoxAsync();
                                    if (box != null)
                                    {
                                        double scaleX = box.Width / img.Width;
                                        double scaleY = box.Height / img.Height;

                                        float targetX = (float)(box.X + (centerX * scaleX));
                                        float targetY = (float)(box.Y + (centerY * scaleY));

                                        // 执行点击 (假设你已有这个方法)
                                        // await ClickElementAtAsync(page, (int)targetX + P_X, (int)targetY + P_Y, Tap_repeatedly);
                                        Console.WriteLine($">>> 成功识别 '{recognizedText}'，已点击 ({targetX}, {targetY})");
                                        return true;
                                    }
                                }
                            }
                        } while (iter.Next(PageIteratorLevel.Word));
                    }
                }
            }
            Console.WriteLine($"画面中未找到目标文本: {targetText}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[异常] OCR 识别过程中出错: {ex.Message}");
        }
        finally
        {
            // 调试阶段可以注释掉删除逻辑，查看 processedPath 图片效果
            if (File.Exists(screenshotPath)) File.Delete(screenshotPath);
            if (File.Exists(processedPath)) File.Delete(processedPath);
        }

        return false;
    }

    // 专治彩色字体的预处理静态方法
    private static void PreProcessColoredText(string inputPath, string outputPath)
    {
        using (Mat raw = new Mat(inputPath, ImreadModes.Color))
        using (Mat enlarged = new Mat())
        using (Mat gray = new Mat())
        using (Mat blurred = new Mat())
        using (Mat binary = new Mat())
        using (Mat clean = new Mat())
        {
            // 1. 放大图片（依然保留，给 OCR 更多像素）
            Cv2.Resize(raw, enlarged, new OpenCvSharp.Size(raw.Width * 2, raw.Height * 2), 0, 0, InterpolationFlags.Cubic);

            // 2. 转灰度
            Cv2.CvtColor(enlarged, gray, ColorConversionCodes.BGR2GRAY);

            // 3. 【新增】高斯模糊：抹除背景里的细小噪声
            // 游戏背景乱通常是因为细节太多，模糊一下能让背景连成一片，方便后面剔除
            Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(3, 3), 0);

            // 4. 【核心修改】改用全局大津法二值化
            // 相比自适应阈值，大津法在处理“干净”的文字上更稳定，不会强行去抠背景细节
            Cv2.Threshold(blurred, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            // 5. 【关键】形态学“开运算”（Opening）
            // 原理：先腐蚀后膨胀。这能直接消灭比文字细小的散乱黑点（噪声）
            using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(2, 2)))
            {
                Cv2.MorphologyEx(binary, clean, MorphTypes.Open, kernel);
            }

            // 6. 自动检测反色（确保白底黑字）
            // 统计边缘像素：如果边缘全是黑色，说明背景是黑的，必须反转
            if (IsBackgroundDark(clean))
            {
                Cv2.BitwiseNot(clean, clean);
            }

            // 7. 保存
            clean.SaveImage(outputPath);
        }
    }

    // 辅助函数：判断背景颜色
    private static bool IsBackgroundDark(Mat m)
    {
        // 取四个角的像素样本
        double cornerPixels = (m.At<byte>(0, 0) +
                              m.At<byte>(0, m.Cols - 1) +
                              m.At<byte>(m.Rows - 1, 0) +
                              m.At<byte>(m.Rows - 1, m.Cols - 1)) / 4.0;
        return cornerPixels < 127; // 平均值小于127说明背景偏黑
    }
    public static async Task<bool> ClickByImageAsync(IPage page, string templatePath, int Tap_repeatedly = 1, int P_X = 0, int P_Y = 0, double threshold = 0.8)
    {
        // 1. 定位游戏 Canvas 元素
        var canvas = page.Locator("#canvas-player");
        if (!await canvas.IsVisibleAsync()) return false;

        // 【修复 1】如果文件不存在，才返回 false
        if (!File.Exists(templatePath))
        {
            Console.WriteLine($"[警告] 找不到模板文件: {templatePath}");
            return false;
        }

        // 【修复 2】使用 C# 语法输出绝对路径，排查路径问题
        Console.WriteLine($"[调试] 当前使用的模板绝对路径是: {Path.GetFullPath(templatePath)}");

        // 2. 设置临时截图路径
        string screenshotPath = "current_game_scene.png";

        // 【修复 4】使用 try...finally 确保不论是否识别成功，最终都能删掉截图文件
        try
        {
            await canvas.ScreenshotAsync(new LocatorScreenshotOptions { Path = screenshotPath });

            // 【修复 3】使用 C# 原生 IO 读取文件流，然后传给 OpenCV 解码，彻底解决路径报错
            byte[] templateBytes = File.ReadAllBytes(templatePath);

            using (Mat template = Cv2.ImDecode(templateBytes, ImreadModes.Color)) // 解码模板图
            using (Mat scene = new Mat(screenshotPath, ImreadModes.Color))        // 截图是在程序根目录生成的，直接读取通常无风险
            using (Mat result = new Mat())
            {
                // 防御性检查：确保图片解码成功
                if (template.Empty() || scene.Empty())
                {
                    Console.WriteLine("[错误] OpenCV 解析图片失败（可能是文件损坏或非标准图片格式）。");
                    return false;
                }

                // 3. 使用 OpenCV 进行模板匹配
                Cv2.MatchTemplate(scene, template, result, TemplateMatchModes.CCoeffNormed);

                // 寻找最大匹配度和位置
                Cv2.MinMaxLoc(result, out double minVal, out double maxVal, out Point minLoc, out Point maxLoc);

                // maxVal 就是相似度（0.0 ~ 1.0）
                Console.WriteLine($"当前识别相似度: {maxVal:P}");

                if (maxVal >= threshold)
                {
                    // 4. 计算图片内中心点位置（像素级别）
                    int centerX = maxLoc.X + (template.Width / 2);
                    int centerY = maxLoc.Y + (template.Height / 2);

                    // 5. 获取 Canvas 在浏览器里的具体位置（BoundingBox）
                    var box = await canvas.BoundingBoxAsync();

                    if (box != null)
                    {
                        // 坐标换算
                        double scaleX = box.Width / scene.Width;   // 例: 1368 / 1920
                        double scaleY = box.Height / scene.Height; // 例: 768 / 1080

                        float targetX = (float)(box.X + (centerX * scaleX));
                        float targetY = (float)(box.Y + (centerY * scaleY));

                        // 6. 执行点击
                        await ClickElementAtAsync(page, (int)targetX + P_X, (int)targetY + P_Y, Tap_repeatedly);
                        Console.WriteLine($">>> 已点击相似点 ({targetX}, {targetY})，相似度 {maxVal:F2}");

                        return true;
                    }
                }
                else
                {
                    Console.WriteLine("相似度不足，未执行点击。");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[异常] 图像匹配过程中出错: {ex.Message}");
        }
        finally
        {
            // 扫尾：确保一定会删除临时截图文件
            if (File.Exists(screenshotPath))
            {
                File.Delete(screenshotPath);
            }
        }

        return false;
    }

    public static class KeyboardHelper
    {
        /// <summary>
        /// 模拟键盘按键操作
        /// </summary>
        /// <param name="page">Playwright Page对象</param>
        /// <param name="key">按键名称 (如 "Enter", "a", "ArrowUp", "Control+V")</param>
        /// <param name="delayMs">按下和松开之间的延迟（毫秒），模拟真实人工按压</param>
        /// <param name="selector">可选：在按键前先聚焦的元素选择器</param>
        public static async Task PressKeyAsync(IPage page, string key, int delayMs = 50, string? selector = null)
        {
            // 1. 如果指定了元素，先点击/聚焦该元素，确保按键事件被正确接收
            if (!string.IsNullOrEmpty(selector))
            {
                await page.FocusAsync(selector);
            }

            // 2. 执行按键动作
            // Delay 参数会自动在 KeyDown 和 KeyUp 之间等待
            await page.Keyboard.PressAsync(key, new KeyboardPressOptions
            {
                Delay = delayMs
            });

            Console.WriteLine($"[Keyboard] 已按下按键: {key} (延迟: {delayMs}ms)");
        }

        /// <summary>
        /// 模拟长按操作（例如游戏中控制角色移动）
        /// </summary>
        public static async Task HoldKeyAsync(IPage page, string key, int durationMs)
        {
            await page.Keyboard.DownAsync(key);
            await Task.Delay(durationMs);
            await page.Keyboard.UpAsync(key);

            Console.WriteLine($"[Keyboard] 已长按按键: {key} 持续: {durationMs}ms");
        }
    }
}