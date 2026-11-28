# Art inventory (2026-11-26, US-271)

Everything the game draws, from the code at `main` (`5e5a4301`) and `sprint/28-business-hub`. Written for the graphics redesign (ADR 0172 §7, spec 032). Sprites are rendered in Blender by `tools/blender/render.py` into `unity/FarmToCity/Assets/Resources/Art/Blender/` (imported uncompressed, max 2048 px); a few things are drawn in code.

**605 Blender sprites** in 10 categories, plus the things drawn in code (section K).

| Category | Sprites |
| --- | --- |
| A. Ground, terrain and boundaries | 48 |
| B. Crops and the kitchen garden | 69 |
| C. Trees and orchards | 22 |
| D. People and portraits | 137 |
| E. Animals, vehicles and machines | 18 |
| F. Your farm: houses, sheds, stores, water | 50 |
| G. Your businesses and stalls | 93 |
| H. The village and town | 85 |
| I. Icons | 76 |
| J. Effects and markers | 7 |
| K. Drawn in code | 8 kinds (below) |

## A. Ground, terrain and boundaries (48)

- **bund_worn** (2): `bund_worn_x`, `bund_worn_y`
- **bund** (2): `bund_x`, `bund_y`
- **corner_stone** (1): `corner_stone`
- **edge_earth** (1): `edge_earth`
- **edge_grass** (1): `edge_grass`
- **edge_paved** (1): `edge_paved`
- **edge_road** (1): `edge_road`
- **edge_soil** (1): `edge_soil`
- **edge_water** (1): `edge_water`
- **fence** (2): `fence_x`, `fence_y`
- **grass_detail** (3): `grass_detail_0`, `grass_detail_1`, `grass_detail_2`
- **ground_bank** (2): `ground_bank_0`, `ground_bank_1`
- **ground_cattle** (1): `ground_cattle`
- **ground_commons** (1): `ground_commons`
- **ground_earth** (1): `ground_earth`
- **ground_grass** (1): `ground_grass`
- **ground_paved** (5): `ground_paved_11`, `ground_paved_12`, `ground_paved_3`, `ground_paved_4`, `ground_paved_8`
- **ground_road** (5): `ground_road_11`, `ground_road_12`, `ground_road_3`, `ground_road_4`, `ground_road_8`
- **ground_scrub** (1): `ground_scrub`
- **ground_soil** (1): `ground_soil`
- **ground_square** (1): `ground_square`
- **ground_village** (2): `ground_village_0`, `ground_village_1`
- **ground_water** (1): `ground_water`
- **hedge_gate** (2): `hedge_gate_x`, `hedge_gate_y`
- **hedge** (2): `hedge_x`, `hedge_y`
- **hill** (3): `hill_0`, `hill_1`, `hill_2`
- **scrub** (1): `scrub`
- **street** (2): `street_x`, `street_y`

## B. Crops and the kitchen garden (69)

- **bed** (1): `bed_empty`
- **bed_garden_coriander** (4): `bed_garden_coriander_g1`, `bed_garden_coriander_g2`, `bed_garden_coriander_g3`, `bed_garden_coriander_ready`
- **bed_garden_radish** (4): `bed_garden_radish_g1`, `bed_garden_radish_g2`, `bed_garden_radish_g3`, `bed_garden_radish_ready`
- **bed_garden_spinach** (4): `bed_garden_spinach_g1`, `bed_garden_spinach_g2`, `bed_garden_spinach_g3`, `bed_garden_spinach_ready`
- **crop_amaranth** (4): `crop_amaranth_g1`, `crop_amaranth_g2`, `crop_amaranth_g3`, `crop_amaranth_ready`
- **crop_berseem** (5): `crop_berseem_cut`, `crop_berseem_g1`, `crop_berseem_g2`, `crop_berseem_g3`, `crop_berseem_ready`
- **crop_lentils** (4): `crop_lentils_g1`, `crop_lentils_g2`, `crop_lentils_g3`, `crop_lentils_ready`
- **crop_maize** (4): `crop_maize_g1`, `crop_maize_g2`, `crop_maize_g3`, `crop_maize_ready`
- **crop_methi** (4): `crop_methi_g1`, `crop_methi_g2`, `crop_methi_g3`, `crop_methi_ready`
- **crop_mustard** (4): `crop_mustard_g1`, `crop_mustard_g2`, `crop_mustard_g3`, `crop_mustard_ready`
- **crop_okra** (4): `crop_okra_g1`, `crop_okra_g2`, `crop_okra_g3`, `crop_okra_ready`
- **crop_peas** (4): `crop_peas_g1`, `crop_peas_g2`, `crop_peas_g3`, `crop_peas_ready`
- **crop_potato** (4): `crop_potato_g1`, `crop_potato_g2`, `crop_potato_g3`, `crop_potato_ready`
- **crop_radish** (4): `crop_radish_g1`, `crop_radish_g2`, `crop_radish_g3`, `crop_radish_ready`
- **crop_rice** (4): `crop_rice_g1`, `crop_rice_g2`, `crop_rice_g3`, `crop_rice_ready`
- **crop_sorghum** (5): `crop_sorghum_cut`, `crop_sorghum_g1`, `crop_sorghum_g2`, `crop_sorghum_g3`, `crop_sorghum_ready`
- **crop_sugarcane** (1): `crop_sugarcane_g2`
- **crop_wheat** (4): `crop_wheat_g1`, `crop_wheat_g2`, `crop_wheat_g3`, `crop_wheat_ready`
- **tree_pit** (1): `tree_pit`

## C. Trees and orchards (22)

- **tree_apple** (4): `tree_apple_mature`, `tree_apple_ripe`, `tree_apple_sapling`, `tree_apple_young`
- **tree_babul** (1): `tree_babul`
- **tree_banana** (4): `tree_banana_mature`, `tree_banana_ripe`, `tree_banana_sapling`, `tree_banana_young`
- **tree_guava** (4): `tree_guava_mature`, `tree_guava_ripe`, `tree_guava_sapling`, `tree_guava_young`
- **tree_mango** (4): `tree_mango_mature`, `tree_mango_ripe`, `tree_mango_sapling`, `tree_mango_young`
- **tree_neem** (1): `tree_neem`
- **tree_papaya** (4): `tree_papaya_mature`, `tree_papaya_ripe`, `tree_papaya_sapling`, `tree_papaya_young`

## D. People and portraits (137)

- **person_boy** (10): `person_boy_back`, `person_boy_back_walk0`, `person_boy_back_walk1`, `person_boy_back_walk2`, `person_boy_back_walk3`, `person_boy_front`, `person_boy_front_walk0`, `person_boy_front_walk1`, `person_boy_front_walk2`, `person_boy_front_walk3`
- **person_elder_f** (10): `person_elder_f_back`, `person_elder_f_back_walk0`, `person_elder_f_back_walk1`, `person_elder_f_back_walk2`, `person_elder_f_back_walk3`, `person_elder_f_front`, `person_elder_f_front_walk0`, `person_elder_f_front_walk1`, `person_elder_f_front_walk2`, `person_elder_f_front_walk3`
- **person_elder_m** (10): `person_elder_m_back`, `person_elder_m_back_walk0`, `person_elder_m_back_walk1`, `person_elder_m_back_walk2`, `person_elder_m_back_walk3`, `person_elder_m_front`, `person_elder_m_front_walk0`, `person_elder_m_front_walk1`, `person_elder_m_front_walk2`, `person_elder_m_front_walk3`
- **person_founder** (12): `person_founder_back`, `person_founder_back_walk0`, `person_founder_back_walk1`, `person_founder_back_walk2`, `person_founder_back_walk3`, `person_founder_back_work`, `person_founder_front`, `person_founder_front_walk0`, `person_founder_front_walk1`, `person_founder_front_walk2`, `person_founder_front_walk3`, `person_founder_front_work`
- **person_girl** (10): `person_girl_back`, `person_girl_back_walk0`, `person_girl_back_walk1`, `person_girl_back_walk2`, `person_girl_back_walk3`, `person_girl_front`, `person_girl_front_walk0`, `person_girl_front_walk1`, `person_girl_front_walk2`, `person_girl_front_walk3`
- **person_hand_f** (12): `person_hand_f_back`, `person_hand_f_back_walk0`, `person_hand_f_back_walk1`, `person_hand_f_back_walk2`, `person_hand_f_back_walk3`, `person_hand_f_back_work`, `person_hand_f_front`, `person_hand_f_front_walk0`, `person_hand_f_front_walk1`, `person_hand_f_front_walk2`, `person_hand_f_front_walk3`, `person_hand_f_front_work`
- **person_hand_m** (12): `person_hand_m_back`, `person_hand_m_back_walk0`, `person_hand_m_back_walk1`, `person_hand_m_back_walk2`, `person_hand_m_back_walk3`, `person_hand_m_back_work`, `person_hand_m_front`, `person_hand_m_front_walk0`, `person_hand_m_front_walk1`, `person_hand_m_front_walk2`, `person_hand_m_front_walk3`, `person_hand_m_front_work`
- **person_labourer_f** (12): `person_labourer_f_back`, `person_labourer_f_back_walk0`, `person_labourer_f_back_walk1`, `person_labourer_f_back_walk2`, `person_labourer_f_back_walk3`, `person_labourer_f_back_work`, `person_labourer_f_front`, `person_labourer_f_front_walk0`, `person_labourer_f_front_walk1`, `person_labourer_f_front_walk2`, `person_labourer_f_front_walk3`, `person_labourer_f_front_work`
- **person_labourer_m** (12): `person_labourer_m_back`, `person_labourer_m_back_walk0`, `person_labourer_m_back_walk1`, `person_labourer_m_back_walk2`, `person_labourer_m_back_walk3`, `person_labourer_m_back_work`, `person_labourer_m_front`, `person_labourer_m_front_walk0`, `person_labourer_m_front_walk1`, `person_labourer_m_front_walk2`, `person_labourer_m_front_walk3`, `person_labourer_m_front_work`
- **person_town_f** (12): `person_town_f_back`, `person_town_f_back_walk0`, `person_town_f_back_walk1`, `person_town_f_back_walk2`, `person_town_f_back_walk3`, `person_town_f_back_work`, `person_town_f_front`, `person_town_f_front_walk0`, `person_town_f_front_walk1`, `person_town_f_front_walk2`, `person_town_f_front_walk3`, `person_town_f_front_work`
- **person_town_m** (12): `person_town_m_back`, `person_town_m_back_walk0`, `person_town_m_back_walk1`, `person_town_m_back_walk2`, `person_town_m_back_walk3`, `person_town_m_back_work`, `person_town_m_front`, `person_town_m_front_walk0`, `person_town_m_front_walk1`, `person_town_m_front_walk2`, `person_town_m_front_walk3`, `person_town_m_front_work`
- **portrait_advisor_bank** (1): `portrait_advisor_bank`
- **portrait_advisor_farm** (1): `portrait_advisor_farm`
- **portrait_advisor_people** (1): `portrait_advisor_people`
- **portrait_advisor_planner** (1): `portrait_advisor_planner`
- **portrait_boy** (1): `portrait_boy`
- **portrait_elder_f** (1): `portrait_elder_f`
- **portrait_elder_m** (1): `portrait_elder_m`
- **portrait_founder** (1): `portrait_founder`
- **portrait_girl** (1): `portrait_girl`
- **portrait_hand_f** (1): `portrait_hand_f`
- **portrait_hand_m** (1): `portrait_hand_m`
- **portrait_town_f** (1): `portrait_town_f`
- **portrait_town_m** (1): `portrait_town_m`

## E. Animals, vehicles and machines (18)

- **animal_buffalo** (3): `animal_buffalo_eat`, `animal_buffalo_lie`, `animal_buffalo_stand`
- **animal_bullock** (3): `animal_bullock_eat`, `animal_bullock_lie`, `animal_bullock_stand`
- **animal_cow** (3): `animal_cow_eat`, `animal_cow_lie`, `animal_cow_stand`
- **plough_team** (1): `plough_team`
- **tempo** (1): `tempo`
- **tractor** (1): `tractor`
- **tractor_cultivator** (1): `tractor_cultivator`
- **tractor_trolley** (1): `tractor_trolley`
- **truck** (2): `truck_back`, `truck_front`
- **van** (2): `van_back`, `van_front`

## F. Your farm: houses, sheds, stores, water (50)

- **canal_end** (1): `canal_end`
- **canal** (1): `canal_x`
- **canal_x** (1): `canal_x_dry`
- **cattle_manger** (1): `cattle_manger`
- **cattle_shed** (2): `cattle_shed`, `cattle_shed_building`
- **chaff_cutter** (1): `chaff_cutter`
- **channel_gate** (1): `channel_gate`
- **channel** (1): `channel_y`
- **channel_y** (1): `channel_y_dry`
- **chhappar** (1): `chhappar`
- **cold_store** (1): `cold_store`
- **compost_pit** (3): `compost_pit_0`, `compost_pit_1`, `compost_pit_2`
- **drip** (1): `drip`
- **family_house** (8): `family_house_building_e`, `family_house_building_n`, `family_house_building_s`, `family_house_building_w`, `family_house_e`, `family_house_n`, `family_house_s`, `family_house_w`
- **granary** (1): `granary`
- **hand_hut** (2): `hand_hut`, `hand_hut_building`
- **haystack** (1): `haystack`
- **hut** (1): `hut`
- **pucca_house** (8): `pucca_house_building_e`, `pucca_house_building_n`, `pucca_house_building_s`, `pucca_house_building_w`, `pucca_house_e`, `pucca_house_n`, `pucca_house_s`, `pucca_house_w`
- **pump_outlet** (1): `pump_outlet`
- **silage_pit** (1): `silage_pit`
- **solar_panel** (1): `solar_panel`
- **straw_stack** (4): `straw_stack_empty`, `straw_stack_high`, `straw_stack_low`, `straw_stack_mid`
- **tubewell** (1): `tubewell`
- **tulsi** (1): `tulsi`
- **water_trough** (1): `water_trough`
- **well** (1): `well`
- **well_pump** (1): `well_pump`
- **woodpile** (1): `woodpile`

## G. Your businesses and stalls (93)

- **bakery** (8): `bakery_building_e`, `bakery_building_n`, `bakery_building_s`, `bakery_building_w`, `bakery_e`, `bakery_n`, `bakery_s`, `bakery_w`
- **brick_kiln** (8): `brick_kiln_building_e`, `brick_kiln_building_n`, `brick_kiln_building_s`, `brick_kiln_building_w`, `brick_kiln_e`, `brick_kiln_n`, `brick_kiln_s`, `brick_kiln_w`
- **dairy** (8): `dairy_building_e`, `dairy_building_n`, `dairy_building_s`, `dairy_building_w`, `dairy_e`, `dairy_n`, `dairy_s`, `dairy_w`
- **flour_mill** (8): `flour_mill_building_e`, `flour_mill_building_n`, `flour_mill_building_s`, `flour_mill_building_w`, `flour_mill_e`, `flour_mill_n`, `flour_mill_s`, `flour_mill_w`
- **kirana** (11): `kirana_building_e`, `kirana_building_n`, `kirana_building_s`, `kirana_building_w`, `kirana_e`, `kirana_n`, `kirana_pitch_1`, `kirana_pitch_2`, `kirana_pitch_3`, `kirana_s`, `kirana_w`
- **newcomer_chai** (4): `newcomer_chai_cart_s`, `newcomer_chai_pitch_1`, `newcomer_chai_pitch_2`, `newcomer_chai_pitch_3`
- **newcomer_meals** (4): `newcomer_meals_cart_s`, `newcomer_meals_pitch_1`, `newcomer_meals_pitch_2`, `newcomer_meals_pitch_3`
- **newcomer_sweets** (4): `newcomer_sweets_cart_s`, `newcomer_sweets_pitch_1`, `newcomer_sweets_pitch_2`, `newcomer_sweets_pitch_3`
- **newcomer_veg** (4): `newcomer_veg_cart_s`, `newcomer_veg_pitch_1`, `newcomer_veg_pitch_2`, `newcomer_veg_pitch_3`
- **pitch_empty** (3): `pitch_empty_1`, `pitch_empty_2`, `pitch_empty_3`
- **restaurant** (11): `restaurant_building_e`, `restaurant_building_n`, `restaurant_building_s`, `restaurant_building_w`, `restaurant_e`, `restaurant_n`, `restaurant_pitch_1`, `restaurant_pitch_2`, `restaurant_pitch_3`, `restaurant_s`, `restaurant_w`
- **sweet_shop** (4): `sweet_shop_pitch_1`, `sweet_shop_pitch_2`, `sweet_shop_pitch_3`, `sweet_shop_s`
- **tea_stall** (4): `tea_stall_pitch_1`, `tea_stall_pitch_2`, `tea_stall_pitch_3`, `tea_stall_s`
- **tuition** (8): `tuition_building_e`, `tuition_building_n`, `tuition_building_s`, `tuition_building_w`, `tuition_e`, `tuition_n`, `tuition_s`, `tuition_w`
- **veg_stall** (4): `veg_stall_pitch_1`, `veg_stall_pitch_2`, `veg_stall_pitch_3`, `veg_stall_s`

## H. The village and town (85)

- **place_banyan** (1): `place_banyan`
- **place_chemist** (1): `place_chemist`
- **place_clinic_plot** (1): `place_clinic_plot`
- **place_collection_plot** (1): `place_collection_plot`
- **place_council_hall** (1): `place_council_hall`
- **place_dealer** (1): `place_dealer`
- **place_drains_plot** (1): `place_drains_plot`
- **place_generator_plot** (1): `place_generator_plot`
- **place_grid_plot** (1): `place_grid_plot`
- **place_grocery** (1): `place_grocery`
- **place_hall_plot** (1): `place_hall_plot`
- **place_house** (2): `place_house_0`, `place_house_1`
- **place_playground_plot** (1): `place_playground_plot`
- **place_pond** (1): `place_pond`
- **place_quarters_plot** (1): `place_quarters_plot`
- **place_school_plot** (1): `place_school_plot`
- **place_tea_stall** (1): `place_tea_stall`
- **place_temple** (1): `place_temple`
- **place_tower_plot** (1): `place_tower_plot`
- **place_watchman_plot** (1): `place_watchman_plot`
- **village_banyan** (1): `village_banyan`
- **village_buffalo** (1): `village_buffalo`
- **village_charpai** (1): `village_charpai`
- **village_chemist** (4): `village_chemist_e`, `village_chemist_n`, `village_chemist_s`, `village_chemist_w`
- **village_clinic** (2): `village_clinic_building_e`, `village_clinic_e`
- **village_collection** (1): `village_collection_e`
- **village_council_hall** (4): `village_council_hall_e`, `village_council_hall_n`, `village_council_hall_s`, `village_council_hall_w`
- **village_cow** (1): `village_cow`
- **village_dairy** (4): `village_dairy_e`, `village_dairy_n`, `village_dairy_s`, `village_dairy_w`
- **village_dealer** (4): `village_dealer_e`, `village_dealer_n`, `village_dealer_s`, `village_dealer_w`
- **village_drains** (1): `village_drains_e`
- **village_generator** (2): `village_generator_e`, `village_generator_off_e`
- **village_grid** (1): `village_grid_e`
- **village_grocery** (4): `village_grocery_e`, `village_grocery_n`, `village_grocery_s`, `village_grocery_w`
- **village_hall** (1): `village_hall_e`
- **village_handpump** (1): `village_handpump`
- **village_haystack** (1): `village_haystack`
- **village_house_0** (4): `village_house_0_e`, `village_house_0_n`, `village_house_0_s`, `village_house_0_w`
- **village_house_1** (4): `village_house_1_e`, `village_house_1_n`, `village_house_1_s`, `village_house_1_w`
- **village_house_2** (4): `village_house_2_e`, `village_house_2_n`, `village_house_2_s`, `village_house_2_w`
- **village_peepal** (1): `village_peepal`
- **village_playground** (1): `village_playground_e`
- **village_pond** (1): `village_pond`
- **village_quarters** (2): `village_quarters_building_e`, `village_quarters_e`
- **village_school** (2): `village_school_building_e`, `village_school_e`
- **village_tea_stall** (4): `village_tea_stall_e`, `village_tea_stall_n`, `village_tea_stall_s`, `village_tea_stall_w`
- **village_temple** (4): `village_temple_e`, `village_temple_n`, `village_temple_s`, `village_temple_w`
- **village_tower** (1): `village_tower_e`
- **village_tree** (1): `village_tree`
- **village_watchman** (1): `village_watchman_e`

## I. Icons (76)

- **icon_animal_buffalo** (1): `icon_animal_buffalo`
- **icon_animal_bullock** (1): `icon_animal_bullock`
- **icon_animal_cow** (1): `icon_animal_cow`
- **icon_basket** (1): `icon_basket`
- **icon_bell** (1): `icon_bell`
- **icon_borewell** (1): `icon_borewell`
- **icon_cart** (1): `icon_cart`
- **icon_channel** (1): `icon_channel`
- **icon_close** (1): `icon_close`
- **icon_codex** (1): `icon_codex`
- **icon_coin** (1): `icon_coin`
- **icon_compost** (1): `icon_compost`
- **icon_deed** (1): `icon_deed`
- **icon_drip** (1): `icon_drip`
- **icon_fertiliser** (1): `icon_fertiliser`
- **icon_flame** (1): `icon_flame`
- **icon_gear** (1): `icon_gear`
- **icon_good_amaranth** (1): `icon_good_amaranth`
- **icon_good_apple** (1): `icon_good_apple`
- **icon_good_atta** (1): `icon_good_atta`
- **icon_good_atta_laddoo** (1): `icon_good_atta_laddoo`
- **icon_good_banana** (1): `icon_good_banana`
- **icon_good_berseem** (1): `icon_good_berseem`
- **icon_good_bran** (1): `icon_good_bran`
- **icon_good_cattle_feed** (1): `icon_good_cattle_feed`
- **icon_good_coriander** (1): `icon_good_coriander`
- **icon_good_curd** (1): `icon_good_curd`
- **icon_good_ghee** (1): `icon_good_ghee`
- **icon_good_green_fodder** (1): `icon_good_green_fodder`
- **icon_good_guava** (1): `icon_good_guava`
- **icon_good_lentils** (1): `icon_good_lentils`
- **icon_good_maize** (1): `icon_good_maize`
- **icon_good_mango** (1): `icon_good_mango`
- **icon_good_methi** (1): `icon_good_methi`
- **icon_good_milk** (1): `icon_good_milk`
- **icon_good_mustard** (1): `icon_good_mustard`
- **icon_good_okra** (1): `icon_good_okra`
- **icon_good_paneer** (1): `icon_good_paneer`
- **icon_good_papaya** (1): `icon_good_papaya`
- **icon_good_pav** (1): `icon_good_pav`
- **icon_good_peas** (1): `icon_good_peas`
- **icon_good_potato** (1): `icon_good_potato`
- **icon_good_radish** (1): `icon_good_radish`
- **icon_good_rasgulla** (1): `icon_good_rasgulla`
- **icon_good_rice** (1): `icon_good_rice`
- **icon_good_rusk** (1): `icon_good_rusk`
- **icon_good_sorghum** (1): `icon_good_sorghum`
- **icon_good_spinach** (1): `icon_good_spinach`
- **icon_good_straw** (1): `icon_good_straw`
- **icon_good_wheat** (1): `icon_good_wheat`
- **icon_happiness** (1): `icon_happiness`
- **icon_health** (1): `icon_health`
- **icon_letter** (1): `icon_letter`
- **icon_megaphone** (1): `icon_megaphone`
- **icon_menu** (1): `icon_menu`
- **icon_orders** (1): `icon_orders`
- **icon_population** (1): `icon_population`
- **icon_pump** (1): `icon_pump`
- **icon_sack** (1): `icon_sack`
- **icon** (1): `icon_sapling`
- **icon_savings** (1): `icon_savings`
- **icon_scroll** (1): `icon_scroll`
- **icon_seed** (1): `icon_seed`
- **icon_shield** (1): `icon_shield`
- **icon_solar_panel** (1): `icon_solar_panel`
- **icon_spray** (1): `icon_spray`
- **icon_star** (3): `icon_star_0`, `icon_star_1`, `icon_star_2`
- **icon_weather_cloudy** (1): `icon_weather_cloudy`
- **icon_weather_frost** (1): `icon_weather_frost`
- **icon_weather_heatwave** (1): `icon_weather_heatwave`
- **icon_weather_heavy_rain** (1): `icon_weather_heavy_rain`
- **icon_weather_rain** (1): `icon_weather_rain`
- **icon_weather_storm** (1): `icon_weather_storm`
- **icon_weather_sun** (1): `icon_weather_sun`

## J. Effects and markers (7)

- **fire_flames** (1): `fire_flames`
- **fire_smoke** (1): `fire_smoke`
- **marker_pest** (1): `marker_pest`
- **marker** (1): `marker_ready`
- **marker_spoil** (1): `marker_spoil`
- **sign_locked** (1): `sign_locked`
- **sign_sale** (1): `sign_sale`

## K. Drawn in code (not Blender)

- **The sky beyond the map:** a flat colour (at the inventory `GameRoot`'s `0x86CDD8`; what is seen past the map is the meadow colour `MapPresenter` sets, ADR 0187).
- **The weather:** rain streaks and splashes (Unity particles), the rain and rime textures, the frost's white crystals (`WeatherPresenter`).
- **The contact shadow** under people, animals and props (`SpriteBank.ContactShadow`).
- **The selection ring** and the **lit acre** for placing (`SpriteBank.SelectionRing`, `TileFill`).
- **The guide arrow** of the first session (`SpriteBank.GuideArrow`).
- **The overlays:** each overlay's tint in five steps, the smoke's red hatch, the Overlays button's picture (`MapPresenter.Overlays`, `SpriteBank.OverlayHatch`, `OverlaysIcon`).
- **The HUD and sheets:** UI Toolkit panels, pills, buttons and badges in the game's colours (`UiKit`), the Baloo 2 and Noto Sans fonts (ADR 0036, 0038).
- **Fly-to icons** and bounces (`Fx`, `Tweens`): they reuse the icons above.

## Proposed redesign stories (sized after the samples, spec 032)

| Story | Category | Points |
| --- | --- | --- |
| US-273 | A. Ground, terrain and boundaries, with the sky and the river's banks | 5 |
| US-274 | B. Crops and the kitchen garden | 5 |
| US-275 | C. Trees and orchards | 3 |
| US-276 | D. People and portraits (walk and work frames) | 8 |
| US-277 | E. Animals, vehicles and machines | 3 |
| US-278 | F. Your farm: houses, sheds, stores, water | 5 |
| US-279 | G. Your businesses and stalls | 5 |
| US-280 | H. The village and town | 5 |
| US-281 | I. Icons, and the code-drawn shadow, ring, arrow and overlay pictures | 3 |
| US-282 | J. Effects and markers, and the weather's look | 3 |

Total about 45 points: Sprints 30–32 at about 15 points each (each story's before/after preview approved before its renders; a `RenderStats` reading each).
