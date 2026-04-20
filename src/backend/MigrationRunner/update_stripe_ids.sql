UPDATE subscription_plans SET
  stripe_price_id = 'price_1TNdFrRyoDZcfmM5oXIJBG0F',
  stripe_product_id = 'prod_UMLxvgzcIKka9D'
WHERE tier = 1;

UPDATE subscription_plans SET
  stripe_price_id = 'price_1TNdG5RyoDZcfmM5XlzkrcLU',
  stripe_product_id = 'prod_UMLxO1ZjtbCh79'
WHERE tier = 2;
