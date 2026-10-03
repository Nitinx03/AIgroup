#include <Servo.h>
#include <SoftwareSerial.h>

Servo servo1, servo2, servo3, servo4;

// HC-06: TXD -> D10 (RX), RXD -> D11 (TX)
SoftwareSerial bluetooth(10, 11);

int angle1 = 90;
int angle2 = 90;
int angle3 = 90;
int angle4 = 90;

void setup() {
  servo1.attach(3);
  servo2.attach(5);
  servo3.attach(6);
  servo4.attach(9);

  servo1.write(angle1);
  servo2.write(angle2);
  servo3.write(angle3);
  servo4.write(angle4);

  bluetooth.begin(9600);
  Serial.begin(9600);
}

void loop() {
  if (bluetooth.available()) {
    String command = bluetooth.readStringUntil('\n');
    command.trim();

    if (command.startsWith("S1:")) {
      angle1 = constrain(command.substring(3).toInt(), 0, 180);
      servo1.write(angle1);
    }
    else if (command.startsWith("S2:")) {
      angle2 = constrain(command.substring(3).toInt(), 0, 180);
      servo2.write(angle2);
    }
    else if (command.startsWith("S3:")) {
      angle3 = constrain(command.substring(3).toInt(), 0, 180);
      servo3.write(angle3);
    }
    else if (command.startsWith("S4:")) {
      angle4 = constrain(command.substring(3).toInt(), 0, 180);
      servo4.write(angle4);
    }
  }
}