# pks5 — Практическое №5 (API) — запросы для Postman (ошибки 400/404/405/415/500)

База (Base URL): `http://localhost:5018`

Важно:
- `GET` **не читает Body**. Если нужно создать сущность — используйте `POST`.
- Для JSON: в Postman выбирайте `Body -> raw -> JSON` (и будет `Content-Type: application/json`).

## 200 OK (проверка, что API работает)
**Method:** `GET`  
**URL:** `http://localhost:5018/api/orders`

## 400 Bad Request

### 400 — невалидное тело (валидация материалов)
**Method:** `POST`  
**URL:** `http://localhost:5018/api/materials`  
**Body (raw / JSON):**
```json
{ "name": "Стекло", "quantity": 10 }
```
Ожидаемо: `400` (нет обязательного поля `unit`/`UnitOfMeasure`).

### 400 — не хватает материалов для заказа
**Method:** `POST`  
**URL:** `http://localhost:5018/api/orders`  
**Body (raw / JSON):**
```json
{ "productName": "Окно", "quantity": 999999, "lineId": 1 }
```
Ожидаемо: `400` с `insufficient_materials`.

### 400 — неверный статус линии
**Method:** `PUT`  
**URL:** `http://localhost:5018/api/lines/1/status`  
**Body (raw / JSON):**
```json
{ "status": "BadStatus" }
```
Ожидаемо: `400`.

## 404 Not Found
**Method:** `GET`  
**URL:** `http://localhost:5018/api/orders/999999/details`  
Ожидаемо: `404`.

## 405 Method Not Allowed
**Method:** `PUT`  
**URL:** `http://localhost:5018/api/materials`  
Ожидаемо: `405`.

## 415 Unsupported Media Type
Этот endpoint принимает JSON или `x-www-form-urlencoded`, но **не** `form-data (multipart)`.

**Method:** `POST`  
**URL:** `http://localhost:5018/api/products`  
**Body:** `form-data` (multipart), поля:
- `name` = `Тестовый продукт`
- `prodTime` = `10`

Ожидаемо: `415 Unsupported Media Type`.

## 500 Internal Server Error (только DEBUG)
**Method:** `GET`  
**URL:** `http://localhost:5018/api/debug/throw`  
Ожидаемо: `500` (в Release будет `404`).