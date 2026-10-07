// =====================================================================
// โปรแกรมทดสอบเซอร์โว (ไม่ใช้บลูทูธ)
//
// ใช้ตรวจว่าการต่อสายเซอร์โวและไฟเลี้ยงถูกต้องหรือไม่
// เซอร์โวจะขยับทีละตัว: ตัวที่ 1 (D3) -> 2 (D5) -> 3 (D6) -> 4 (D9) แล้ววนใหม่
// เปิด Serial Monitor (9600 baud) เพื่อดูว่าตอนนี้กำลังทดสอบตัวไหน
//
// ฐานหมุนขยับช่วง 45-135 องศา, ไหล่และข้อศอกขยับช่วง 10-90 องศา (ขยับได้จริงไม่เกิน 100)
// ส่วนมือจับขยับแค่ 0-45 องศา ถ้าสั่งเกินช่วงที่ขยับได้จริงจะดันชนโครงจนเซอร์โวร้อน
// ทดสอบเสร็จแล้วอย่าลืมอัปโหลด RobotArm_BT4.ino กลับลงบอร์ด
// =====================================================================

#include <Servo.h>

Servo servos[4];
const int pins[4] = {3, 5, 6, 9};                       // ขาสัญญาณของเซอร์โวแต่ละตัว
const char* names[4] = {"1 Base (D3)", "2 Shoulder (D5)", "3 Elbow (D6)", "4 Gripper (D9)"};

// ช่วงองศาที่ใช้ทดสอบของแต่ละตัว: ต่ำสุด, สูงสุด, ตำแหน่งพัก
const int lowAngle[4]  = {45, 10, 10, 0};
const int highAngle[4] = {135, 90, 90, 45};
const int restAngle[4] = {90, 0, 0, 0};

void setup() {
  Serial.begin(9600);
  for (int i = 0; i < 4; i++) {
    servos[i].attach(pins[i]);
    servos[i].write(restAngle[i]);
  }
  Serial.println("Servo test start");
}

void loop() {
  for (int i = 0; i < 4; i++) {
    Serial.print("Testing servo ");
    Serial.println(names[i]);

    servos[i].write(lowAngle[i]);
    delay(700);
    servos[i].write(highAngle[i]);
    delay(700);
    servos[i].write(restAngle[i]);
    delay(700);
  }
}
