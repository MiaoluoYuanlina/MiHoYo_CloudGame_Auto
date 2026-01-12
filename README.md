# MiHoYo_CloudGame_Auto
通过python控制Chrome浏览器自动启动米哈游云游戏，通过识别图像，注入javascript，来自动做每日任务。目前只实现了 崩坏星穹铁道 的自动化。


你想要预先在Windows设备上安装完Chrome浏览器，第一次运行你需要手动登入。

编辑main.py

参数1 保存cookies
参数2 每日体力刷什么，识别的是你游戏里设置的要培养的角色 填写:培养_1 培养_2 培养_3 隧洞 位面
main_StarRail.Set_Config("mihoyo_cookies_StarRail.pkl","隧洞")

开始执行
main_StarRail.main()
