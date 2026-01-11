import pickle
import os
import time
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from selenium.common.exceptions import TimeoutException
import cv2
import numpy as np
import time
from selenium.webdriver.common.action_chains import ActionChains



COOKIE_FILE = "mihoyo_cookies.pkl"
AD_SELECTORS = [".van-overlay", ".clg-collect-guide-popup"]
START_GAME_BTN = ".wel-card__content--start"


def Set_Config(cookie,SELECTORS,STARTAMEBTN):
    global COOKIE_FILE, AD_SELECTORS, START_GAME_BTN
    COOKIE_FILE = cookie
    AD_SELECTORS = SELECTORS
    START_GAME_BTN = STARTAMEBTN


def clean_page_elements(driver):
    """使用 JavaScript 强力删除遮罩层和广告弹窗"""
    print(">>> 正在清理页面遮罩与弹窗...")
    selectors_str = ", ".join([f"'{s}'" for s in AD_SELECTORS])
    script = f"""
    var selectors = [{selectors_str}];
    selectors.forEach(selector => {{
        var elements = document.querySelectorAll(selector);
        elements.forEach(el => el.remove());
    }});
    """
    driver.execute_script(script)

def save_cookies(driver):
    """保存当前登录状态到本地"""
    with open(COOKIE_FILE, 'wb') as file:
        pickle.dump(driver.get_cookies(), file)
    print(f"\n[系统] 登录成功！Cookie 已保存至 {COOKIE_FILE}")

def load_cookies(driver):
    """加载本地 Cookie"""
    if os.path.exists(COOKIE_FILE):
        with open(COOKIE_FILE, 'rb') as file:
            cookies = pickle.load(file)
            for cookie in cookies:
                if 'expiry' in cookie:
                    del cookie['expiry']
                driver.add_cookie(cookie)
        print("[系统] 已注入本地 Cookie。")
        return True
    return False

def scroll_wheel(driver, delta_y=500):
    """
    delta_y: 正数向下滚动，负数向上滚动
    """
    actions = ActionChains(driver)
    # 直接在当前鼠标位置滚动
    actions.scroll_by_amount(0, delta_y).perform()
    print(f"[信息] 滚轮滚动了 {delta_y} 像素")

def click_by_percent(driver, px, py):
    if not hasattr(driver, "execute_script"):
        print("[错误] 必须传入 driver 实例")
        return

    size = driver.execute_script("return { w: window.innerWidth, h: window.innerHeight };")
    sw, sh = size['w'], size['h']
    final_x = int(sw * px)
    final_y = int(sh * py)

    # 使用更底层的 MouseEvent 构造函数
    js_code = f"""
    var x = {final_x};
    var y = {final_y};

    // 1. 红点调试
    var dot = document.createElement('div');
    dot.style.cssText = "position:absolute; width:12px; height:12px; background:red; border:2px solid white; border-radius:50%; z-index:999999; pointer-events:none; left:" + (x-6) + "px; top:" + (y-6) + "px;";
    document.body.appendChild(dot);
    setTimeout(() => dot.remove(), 2000);

    // 2. 找到该坐标下的元素
    var el = document.elementFromPoint(x, y);
    if (el) {{
        // 3. 构造并按顺序发送：按下、抬起、单击
        var opts = {{ bubbles: true, cancelable: true, view: window, clientX: x, clientY: y }};
        el.dispatchEvent(new MouseEvent('mousedown', opts));
        el.dispatchEvent(new MouseEvent('mouseup', opts));
        el.dispatchEvent(new MouseEvent('click', opts));

        // 针对云游戏的特殊处理：如果存在输入框层，尝试聚焦
        el.focus(); 
    }}
    """
    driver.execute_script(js_code)
    print(f"执行原生模拟点击: ({final_x}, {final_y})")

def click_at_pixel(driver, x, y):
    """
    直接点击网页内的绝对像素坐标
    :param x: 横向像素值 (从视口左侧开始算)
    :param y: 纵向像素值 (从视口顶部开始算)
    """
    if not hasattr(driver, "execute_script"):
        print("[错误] 第一个参数必须是 driver 实例")
        return

    # 注入红点调试和原生点击事件
    js_code = f"""
    var targetX = {x};
    var targetY = {y};

    // 1. 在点击位置显示红点，方便你肉眼确认位置是否正确
    var dot = document.createElement('div');
    dot.style.cssText = "position:absolute; width:10px; height:10px; background:red; border-radius:50%; z-index:999999; pointer-events:none;";
    dot.style.left = (targetX - 5) + 'px';
    dot.style.top = (targetY - 5) + 'px';
    document.body.appendChild(dot);
    setTimeout(() => dot.remove(), 2000);

    // 2. 获取该像素点下的元素
    var el = document.elementFromPoint(targetX, targetY);
    if (el) {{
        // 3. 构造完整的鼠标点击序列（按下 -> 抬起 -> 单击）
        var opts = {{ 
            bubbles: true, 
            cancelable: true, 
            view: window, 
            clientX: targetX, 
            clientY: targetY,
            buttons: 1 
        }};
        el.dispatchEvent(new MouseEvent('mousedown', opts));
        el.dispatchEvent(new MouseEvent('mouseup', opts));
        el.dispatchEvent(new MouseEvent('click', opts));
        console.log("已点击元素:", el);
    }} else {{
        console.log("该坐标点没有可点击元素");
    }}
    """
    driver.execute_script(js_code)
    print(f"[操作] 尝试点击固定像素坐标: ({x}, {y})")

def get_image_coordinates(driver, target_img_path, threshold=0.8):
    """
    仅识别图片并返回其在视口中的中心坐标 (x, y)
    :return: (target_x, target_y) 或 None
    """
    # 1. 获取当前视口截图
    temp_screenshot = "search_cache.png"
    driver.save_screenshot(temp_screenshot)

    # 2. 读取截图和目标素材 (支持中文路径)
    screen = cv2.imdecode(np.fromfile(temp_screenshot, dtype=np.uint8), cv2.IMREAD_COLOR)
    target = cv2.imdecode(np.fromfile(target_img_path, dtype=np.uint8), cv2.IMREAD_COLOR)

    if screen is None or target is None:
        print(f"[错误] 无法读取文件，请检查: {target_img_path}")
        # 这里应该返回 None 或者抛出异常，取决于你的函数定义
        # return None

    # 3. 执行模板匹配
    result = cv2.matchTemplate(screen, target, cv2.TM_CCOEFF_NORMED)
    _, max_val, _, max_loc = cv2.minMaxLoc(result)

    # 4. 判断并返回坐标
    if max_val >= threshold:
        # h, w 是目标图片的告诉和宽度
        h, w = target.shape[:2]

        # --- 获取用于绘制框的关键数据 ---
        # 框的左上角 X 坐标
        box_x = max_loc[0]
        # 框的左上角 Y 坐标
        box_y = max_loc[1]
        # 框的宽度
        box_w = w
        # 框的高度
        box_h = h

        # --- 依然计算中心点用于点击 ---
        target_x = max_loc[0] + w // 2
        target_y = max_loc[1] + h // 2

        # 注入 JS: 绘制矩形框并点击中心
        # 修改点：js_code 的内容
        js_code = f"""
            // 点击所需的中心坐标
            var clickX = {target_x};
            var clickY = {target_y};

            // 绘制框所需的左上角坐标和宽高
            var boxX = {box_x};
            var boxY = {box_y};
            var boxW = {box_w};
            var boxH = {box_h};

            // 1. 在识别到的区域绘制一个矩形框
            var box = document.createElement('div');
            // CSS 设置:
            // position: absolute - 绝对定位
            // border: 3px solid #00FF00 - 设置一个显眼的绿色边框 (你可以改成喜欢的颜色，比如之前的 #FA00FF)
            // background: transparent - 背景透明，只显示框
            // box-sizing: border-box - 确保边框宽度包含在总宽高内，定位更准确
            // z-index: 999999 - 确保显示在最上层
            // pointer-events: none - 确保框不会阻挡鼠标点击事件穿透下去
            box.style.cssText = "position:absolute; border:2px solid #00FF00; background:transparent; box-sizing: border-box; z-index:999999; pointer-events:none;";

            // 设置框的位置和大小
            box.style.left = boxX + 'px';
            box.style.top = boxY + 'px';
            box.style.width = boxW + 'px';
            box.style.height = boxH + 'px';

            document.body.appendChild(box);

            // 2秒后自动移除框
            setTimeout(() => box.remove(), 2000);

            // 2. 获取中心像素点下的元素 (维持原有的点击逻辑)
            var el = document.elementFromPoint(clickX, clickY);
            if (el) {{
                // 3. 构造完整的鼠标点击序列（按下 -> 抬起 -> 单击）
                var opts = {{
                    bubbles: true,
                    cancelable: true,
                    view: window,
                    clientX: clickX,
                    clientY: clickY,
                    buttons: 1
                }};
                el.dispatchEvent(new MouseEvent('mousedown', opts));
                el.dispatchEvent(new MouseEvent('mouseup', opts));
                el.dispatchEvent(new MouseEvent('click', opts));
                console.log("已点击中心元素:", el);
            }} else {{
                console.log("该坐标点没有可点击元素");
            }}
            """
        driver.execute_script(js_code)

        print(f"[信息] {target_img_path} | 置信度: {max_val:.2f} | 区域: ({box_x},{box_y}) {box_w}x{box_h}")
        return (target_x, target_y)

    print(f"[信息] {target_img_path} | 最高匹配度: {max_val:.2f}")
    # return None

def image_click(driver, targets, DX=0, DY=0):
    for item in targets:
        print(f">>> 尝试寻找: {item['desc']}")
        # 调用函数
        success = get_image_coordinates(driver, item['path'], threshold=0.8)

        print(f">>>  {success}")
        if success:
            click_at_pixel(driver, success[0]+DX, success[1]+DY)
        else:
            # 如果没找到，可以选则继续找下一个，或者重试
            pass