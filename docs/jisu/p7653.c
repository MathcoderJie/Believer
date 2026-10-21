#include <stdio.h>
int main(){int y,m,d,days[13]={0,31,28,31,30,31,30,31,31,30,31,30,31};int i,total=0,leap;scanf("%d%d%d",&y,&m,&d);if(y<=0||m<1||m>12||d<1){puts("data error!");return 0;}leap=(y%4==0&&y%100!=0)||y%400==0;if(leap)days[2]=29;if(d>days[m]){puts("data error!");return 0;}for(i=1;i<m;i++)total+=days[i];total+=d;printf("%d\n",total);return 0;}
