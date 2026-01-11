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

import Universal

# --- 配置区 ---
URL = "https://zzz.mihoyo.com/cloud"
COOKIE_FILE = "mihoyo_cookies.pkl"

#培养_1 培养_2 培养_3 隧洞 位面
GAME_Brush_Material_Type = "隧洞"

# 选择器配置
START_GAME_BTN = ".wel-card__content--start"
AD_SELECTORS = [".van-overlay", ".clg-collect-guide-popup"]


def Set_Config(cookie,GAME_Brush_Material_Type):
    """
        配置游戏脚本运行参数。

        cookie:
            读取的cookie

        GAME_Brush_Material_Type:
            自动刷的材料类型
            填写:

    """
    global COOKIE_FILE, GAME_BRUSH_MATERIAL_TYPE
    GAME_BRUSH_MATERIAL_TYPE = GAME_Brush_Material_Type
    COOKIE_FILE = cookie




def main():
    Universal.Set_Config(COOKIE_FILE,GAME_BRUSH_MATERIAL_TYPE)

    chrome_options = Options()
    chrome_options.add_argument("--disable-blink-features=AutomationControlled")

    # 强制浏览器按 1:1 像素渲染，无视 Windows 的缩放设置
    chrome_options.add_argument("--force-device-scale-factor=1")
    chrome_options.add_argument("--high-dpi-support=1")

    # 设置固定的窗口大小（必须和你截图制作 target.png 时的大小一致）
    driver = webdriver.Chrome(options=chrome_options)
    driver.set_window_size(1024, 768)
    #driver.maximize_window()


    try:
        driver.get(URL)
        if Universal.load_cookies(driver):
            driver.refresh()
            time.sleep(2)

        wait = WebDriverWait(driver, 15)

        # 1. 检测并进入主界面登录态
        try:
            start_btn = wait.until(EC.presence_of_element_located((By.CSS_SELECTOR, START_GAME_BTN)))
        except TimeoutException:
            print("\n[信息] 未检测到登录状态，请手动登录。")
            while True:
                btns = driver.find_elements(By.CSS_SELECTOR, START_GAME_BTN)
                if len(btns) > 0 and btns[0].is_displayed():
                    start_btn = btns[0]
                    Universal.save_cookies(driver)
                    break
                time.sleep(2)

        # 2. 点击进入游戏
        Universal.clean_page_elements(driver)
        time.sleep(0.5)
        driver.execute_script("arguments[0].click();", start_btn)
        print("[信息] 已点击“进入游戏”。")

        # 3. 处理免费节点选择
        print("[信息] 正在检查是否需要选择普通队列...")
        try:
            # 查找包含“普通队列”文本的元素
            queue_xpath = "//*[contains(text(), '普通队列')]"
            free_node = WebDriverWait(driver, 5).until(
                EC.element_to_be_clickable((By.XPATH, queue_xpath))
            )
            driver.execute_script("arguments[0].click();", free_node)
            print("[信息] 已自动选择“普通队列”。")
        except TimeoutException:
            print("[警告] 未发现队列选择弹窗，继续后续流程。")

        # 4. 循环检测是否成功进入游戏
        print("[信息] 正在监控游戏加载状态，请稍候（正在排队或加载资源）...")

        # 定义成功标志的选择器
        SUCCESS_SIGNAL = ".game-menu-setting-guide__main"

        entered_game = False
        start_time = time.time()  # 记录开始时间（可选，用于计算排队时长）

        while not entered_game:
            try:
                # 尝试寻找成功标志
                # 使用 find_elements 避免找不到时抛出异常导致程序崩溃
                success_elements = driver.find_elements(By.CSS_SELECTOR, SUCCESS_SIGNAL)

                if len(success_elements) > 0 and success_elements[0].is_displayed():
                    elapsed = int(time.time() - start_time)
                    print(f"\n[信息] 检测到游戏菜单引导，已正式进入游戏！(耗时: {elapsed}s)")
                    entered_game = True
                    break

                # 如果没进去，再顺便检查一下中途是否又弹出了协议（防止流程卡住）
                try:
                    agreement_xpath = "//button[contains(@class, 'van-dialog__confirm')]//span[contains(text(), '接受')]"
                    re_accept = driver.find_elements(By.XPATH, agreement_xpath)
                    if len(re_accept) > 0 and re_accept[0].is_displayed():
                        driver.execute_script("arguments[0].click();", re_accept[0])
                        print("\n[信息] 捕获并处理了中途弹出的协议。")
                except:
                    pass

                # 在控制台打印心跳，证明程序没死
                print(".", end="", flush=True)
                time.sleep(3)  # 每3秒检测一次，避免过度占用CPU

            except Exception as e:
                print(f"\n[警告] 监控中出现微小波动: {e}")
                time.sleep(2)

                # 5. 进入游戏后的扫尾工作：等待 20 秒检测并点击最终的“接受”按钮
                print("[信息] 正在扫描用户协议对话框")
                try:
                    # 这里的 XPATH 对应你提供的 <button> 结构
                    # 重点在于包含 '接受' 文本的按钮内容
                    final_accept_xpath = "//button[contains(@class, 'van-dialog__confirm')]//span[contains(text(), '接受')]"

                    # 使用 WebDriverWait 等待最多 20 秒
                    final_accept_btn = WebDriverWait(driver, 20).until(
                        EC.element_to_be_clickable((By.XPATH, final_accept_xpath))
                    )

                    # 执行强力点击
                    driver.execute_script("arguments[0].click();", final_accept_btn)
                    print("[信息] 已点击最终的“接受”按钮，清除弹窗。")

                except TimeoutException:
                    # 20秒内没出现则说明没有弹窗干扰
                    print("[警告] 20秒内未发现多余弹窗，流程正常。")


        # 5. 清理引导信息

        try:
            # 定义 JS 脚本：遍历页面所有 span，找到内容为“接受”的并点击其父级按钮
            js_click_accept = """
                    var spans = document.querySelectorAll('span.van-button__text');
                    var clicked = false;
                    for (var i = 0; i < spans.length; i++) {
                        if (spans[i].textContent.trim() === '接受') {
                            // 向上找最近的 button 标签并点击
                            var btn = spans[i].closest('button');
                            if (btn) {
                                btn.click();
                                clicked = true;
                                break;
                            }
                        }
                    }
                    return clicked;
                    """

            # 持续检测 20 秒
            found = False
            for _ in range(10):  # 每 2 秒试一次，共 20 秒
                if driver.execute_script(js_click_accept):
                    print("[信息] 已通过 JS 点击了“接受”按钮。")
                    found = True
                    break
                time.sleep(2)

            if not found:
                print("[信息] 20秒内未发现“接受”按钮，可能已自动消失或未弹出。")

                # --- 6. 移除游戏设置引导遮罩 ---
                print("[信息] 检查并清理游戏操作引导...")
                try:
                    # 定义 JS 脚本：直接查找该引导的最外层 class 并删除
                    js_remove_guide = """
                    var guide = document.querySelector('.game-menu-setting-guide');
                    if (guide) {
                        guide.remove();
                        return true;
                    }
                    return false;
                    """

                    # 尝试执行删除
                    if driver.execute_script(js_remove_guide):
                        print("[信息] 已强制删除游戏操作引导遮罩。")
                    else:
                        # 如果没找到，可能它还没加载出来，稍微等一下再试一次
                        time.sleep(2)
                        if driver.execute_script(js_remove_guide):
                            print("[信息] 延迟检测并删除了引导遮罩。")
                        else:
                            print("[警告] 未发现引导遮罩，可能已手动关闭或未触发。")

                except Exception as e:
                    print(f"[错误] 清理引导遮罩时出现异常: {e}")


        except Exception as e:
            print(f"[错误] 点击“接受”时出现非预期错误: {e}")


        # --- 7. 移除游戏设置引导遮罩 ---
        print("[信息] 检查并清理游戏操作引导...")
        try:
            # 定义 JS 脚本：直接查找该引导的最外层 class 并删除
            js_remove_guide = """
                    var guide = document.querySelector('.game-menu-setting-guide');
                    if (guide) {
                        guide.remove();
                        return true;
                    }
                    return false;
                    """

            # 尝试执行删除
            if driver.execute_script(js_remove_guide):
                print("[信息] 已强制删除游戏操作引导遮罩。")
            else:
                # 如果没找到，可能它还没加载出来，稍微等一下再试一次
                time.sleep(2)
                if driver.execute_script(js_remove_guide):
                    print("[信息] 延迟检测并删除了引导遮罩。")
                else:
                    print("[信息] 未发现引导遮罩，可能已手动关闭或未触发。")

        except Exception as e:
            print(f"[信息] 清理引导遮罩时出现异常: {e}")


        # --- 8. 检测 Canvas 画面并点击进入游戏内容 ---
        print("[信息] 游戏画面已加载。")

        try:
            time.sleep(8.5)
            print("[信息] 正在尝试点击“点击进入”...")

            for i in range(5):
                Universal.click_by_percent(driver, 0.5, 0.5)
                time.sleep(2)
                # 检查引导是否消失，或者画面是否变化
                print(f"[信息]尝试点击次数: {i + 1}/5")

            print("[信息] 已向游戏画面中心发送点击事件。")

        except Exception as e:
            print(f"[错误] 在 Canvas 阶段点击失败: {e}")

        # --- 9. 点击 F4 ---
        print("[信息] 等待 7 秒确保游戏环境稳定...")
        time.sleep(7)









    except Exception as e:
        print(f"\n[异常] 程序出错: {e}")
    finally:
        driver.quit()






if __name__ == "__main__":
    main()