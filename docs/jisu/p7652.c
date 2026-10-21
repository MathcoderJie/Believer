#include <stdio.h>
int main(){double p,w,s,d;int k;scanf("%lf%lf%lf",&p,&w,&s);k=(int)(s/250.0);switch(k){case 0:d=0.0;break;case 1:d=0.02;break;case 2:case 3:d=0.05;break;case 4:case 5:case 6:case 7:d=0.08;break;case 8:case 9:case 10:case 11:d=0.10;break;default:d=0.15;break;}printf("%.2f\n",p*w*s*(1.0-d));return 0;}
