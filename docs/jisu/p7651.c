#include <stdio.h>
#include <ctype.h>
int main(){char c;scanf(" %c",&c);switch(toupper((unsigned char)c)){case 'A':puts("Very good!");break;case 'B':puts("Good!");break;case 'C':puts("OK!");break;case 'D':puts("Bad!");break;case 'E':puts("Too bad!");break;}return 0;}
