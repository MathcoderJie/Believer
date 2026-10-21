#include <stdio.h>
int main(){int y;scanf("%d",&y);if((y%4==0&&y%100!=0)||y%400==0)puts("Y");else puts("N");return 0;}
