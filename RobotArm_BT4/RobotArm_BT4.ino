// =====================================================================
// แขนกล 4 แกน ควบคุมผ่านบลูทูธ (Arduino Nano + โมดูลบลูทูธ + เซอร์โว 4 ตัว)
//
// แอปบนมือถือจะส่งคำสั่งมาเป็นข้อความ 1 บรรทัด รูปแบบ "S<เลขเซอร์โว>:<องศา>"
// เช่น "S1:120" = สั่งเซอร์โวตัวที่ 1 ไปที่ 120 องศา (ปิดท้ายด้วย \n)
//   S1 = ฐานหมุน   S2 = ไหล่   S3 = ข้อศอก   S4 = มือจับ
//
// ใช้ได้ทั้ง HC-06 (บลูทูธแบบ Classic) และโมดูล BLE แบบ HM-10 / AT-09 / BT05
// เพราะทั้งสองแบบส่งข้อความต่อมาที่ Nano ทางขา TX/RX เหมือนกัน
// =====================================================================

#include <Servo.h>           // ไลบรารีควบคุมเซอร์โว (มีมากับ Arduino IDE)
#include <SoftwareSerial.h>  // ไลบรารีจำลองพอร์ตอนุกรมบนขาดิจิทัลทั่วไป

// สร้างตัวแปรเซอร์โว 4 ตัว
Servo servo1, servo2, servo3, servo4;

// การต่อสายโมดูลบลูทูธ:
//   TXD ของโมดูล -> ขา D10 ของ Nano (ขารับข้อมูล RX)
//   RXD ของโมดูล -> ขา D11 ของ Nano (ขาส่งข้อมูล TX)
// ใช้ D10/D11 แทน D0/D1 เพื่อไม่ให้ชนกับสาย USB ตอนอัปโหลดโค้ด
SoftwareSerial bluetooth(10, 11);

// เก็บองศาปัจจุบันของเซอร์โวแต่ละตัว (เริ่มต้นที่ 90 องศา = ตรงกลาง)
int angle1 = 90;  // ฐานหมุน
int angle2 = 90;  // ไหล่
int angle3 = 90;  // ข้อศอก
int angle4 = 90;  // มือจับ

// setup() ทำงานครั้งเดียวตอนเปิดบอร์ดหรือกดรีเซ็ต
void setup() {
  // ผูกเซอร์โวแต่ละตัวเข้ากับขา PWM ของ Nano
  servo1.attach(3);  // ฐานหมุน -> D3
  servo2.attach(5);  // ไหล่     -> D5
  servo3.attach(6);  // ข้อศอก   -> D6
  servo4.attach(9);  // มือจับ   -> D9

  // สั่งเซอร์โวทุกตัวไปที่ตำแหน่งเริ่มต้น
  servo1.write(angle1);
  servo2.write(angle2);
  servo3.write(angle3);
  servo4.write(angle4);

  bluetooth.begin(9600);  // ความเร็วสื่อสารกับโมดูลบลูทูธ (ค่าเริ่มต้นของโมดูลคือ 9600)
  Serial.begin(9600);     // พอร์ต USB สำหรับดูข้อมูลใน Serial Monitor
}

// loop() ทำงานวนซ้ำตลอดเวลา
void loop() {
  // ตรวจว่ามีข้อมูลจากบลูทูธเข้ามาหรือไม่
  if (bluetooth.available()) {
    // อ่านข้อความจนถึงตัวขึ้นบรรทัดใหม่ (\n) เช่น "S1:120"
    String command = bluetooth.readStringUntil('\n');
    command.trim();  // ตัดช่องว่างและตัวขึ้นบรรทัด (\r) หัวท้ายทิ้ง

    // ดูว่าเป็นคำสั่งของเซอร์โวตัวไหน แล้วอ่านตัวเลของศาหลัง "Sx:"
    // substring(3) = ข้อความตั้งแต่ตัวที่ 4 เป็นต้นไป (ตัวเลของศา)
    // constrain(..., 0, 180) = บังคับให้อยู่ในช่วง 0-180 องศา กันเซอร์โวเสียหาย
    if (command.startsWith("S1:")) {         // ฐานหมุน
      angle1 = constrain(command.substring(3).toInt(), 0, 180);
      servo1.write(angle1);
    }
    else if (command.startsWith("S2:")) {    // ไหล่
      angle2 = constrain(command.substring(3).toInt(), 0, 180);
      servo2.write(angle2);
    }
    else if (command.startsWith("S3:")) {    // ข้อศอก
      angle3 = constrain(command.substring(3).toInt(), 0, 180);
      servo3.write(angle3);
    }
    else if (command.startsWith("S4:")) {    // มือจับ
      angle4 = constrain(command.substring(3).toInt(), 0, 180);
      servo4.write(angle4);
    }
    // คำสั่งที่ไม่ตรงรูปแบบจะถูกข้ามไป
  }
}
