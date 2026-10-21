#include <stdio.h>
int main(){double a,b,c;int x,y,z,t;scanf("%lf%lf%lf",&a,&b,&c);x=(int)a;y=(int)b;z=(int)c;if(x>y){t=x;x=y;y=t;}if(y>z){t=y;y=z;z=t;}if(x>y){t=x;x=y;y=t;}printf("%d %d %d\n",x,y,z);return 0;}
