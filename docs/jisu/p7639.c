#include <stdio.h>
#include <math.h>
int main(){double a,b,c,d,x1,x2,t;scanf("%lf%lf%lf",&a,&b,&c);d=b*b-4*a*c;x1=(-b+sqrt(d))/(2*a);x2=(-b-sqrt(d))/(2*a);if(x1<x2){t=x1;x1=x2;x2=t;}if(fabs(x1)<0.0000001)x1=0;if(fabs(x2)<0.0000001)x2=0;printf("x1=%.2f\nx2=%.2f\n",x1,x2);return 0;}
