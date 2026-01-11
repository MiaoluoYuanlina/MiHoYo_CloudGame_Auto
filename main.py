import main_StarRail
import main_ZenlessZoneZero
import main_GenshinImpact

# pyinstaller -F -c main.py

def main():

    main_StarRail.Set_Config("mihoyo_cookies_StarRail_227225495.pkl","隧洞")
    main_StarRail.main()

    #main_ZenlessZoneZero.Set_Config("mihoyo_cookies_ZenlessZoneZero_227225495.pkl","")
    #main_ZenlessZoneZero.main()

    #main_GenshinImpact.Set_Config("mihoyo_cookies_GenshinImpact_227225495.pkl","")
    #main_GenshinImpact.main()

    main_StarRail.Set_Config("mihoyo_cookies_StarRail_317341552.pkl", "隧洞")
    main_StarRail.main()

    # main_ZenlessZoneZero.Set_Config("mihoyo_cookies_ZenlessZoneZero_317341552.pkl","")
    # main_ZenlessZoneZero.main()

    # main_GenshinImpact.Set_Config("mihoyo_cookies_GenshinImpact_317341552.pkl","")
    # main_GenshinImpact.main()

if __name__ == "__main__":
    main()