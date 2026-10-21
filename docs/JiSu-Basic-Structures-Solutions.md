# JiSu 基本结构：顺序结构 + 分支结构 全部题目解答

> 课程页面：CITEL 计算机类 C 程序设计课程-公共课-2026

> 范围：顺序结构 8 题 + 分支结构 9 题 = 17 题。

> 平台提交结果：**17/17 AC，全部通过**。

> 语言：C（gcc）。以下代码均已在课程平台提交并通过评测。

## 评测汇总

| # | 题号 | 题目 | 测试点 | 结果 |
|---:|---:|---|---:|---|
| 1 | 7636 | 魔法石检验 | 4 | **AC** |
| 2 | 7637 | 小数的四舍五入 | 4 | **AC** |
| 3 | 7638 | 三角形的面积 | 4 | **AC** |
| 4 | 7639 | 一元二次方程的解 | 4 | **AC** |
| 5 | 7640 | 甲流疫情死亡率 | 4 | **AC** |
| 6 | 7641 | 多项式的值 | 5 | **AC** |
| 7 | 7642 | 并联电阻的阻值 | 5 | **AC** |
| 8 | 7643 | 温度转换 | 3 | **AC** |
| 9 | 7645 | 绝对值 | 3 | **AC** |
| 10 | 7646 | 三实数排序 | 4 | **AC** |
| 11 | 7647 | 等级制 | 8 | **AC** |
| 12 | 7648 | 一元二次方程的解II | 6 | **AC** |
| 13 | 7649 | 闰年 | 6 | **AC** |
| 14 | 7650 | 五分制 | 5 | **AC** |
| 15 | 7651 | 等级划分 | 5 | **AC** |
| 16 | 7652 | 快递费 | 8 | **AC** |
| 17 | 7653 | 第几天 | 13 | **AC** |

合计：**17 题全部 AC，91 个测试点全部通过**。

## 源码文件

所有代码已保存在 `docs/jisu/p<题目ID>.c`。

```text
p7636.c    魔法石检验
p7637.c    小数的四舍五入
p7638.c    三角形的面积
p7639.c    一元二次方程的解
p7640.c    甲流疫情死亡率
p7641.c    多项式的值
p7642.c    并联电阻的阻值
p7643.c    温度转换
p7645.c    绝对值
p7646.c    三实数排序
p7647.c    等级制
p7648.c    一元二次方程的解II
p7649.c    闰年
p7650.c    五分制
p7651.c    等级划分
p7652.c    快递费
p7653.c    第几天
```

## 7636 · 魔法石检验

```c
#include <stdio.h>
int main(){int n;scanf("%d",&n);printf("%d %d %d\n",n/100,n/10%10,n%10);return 0;}
```

## 7637 · 小数的四舍五入

```c
#include <stdio.h>
#include <math.h>
int main(){double x;scanf("%lf",&x);printf("%.2f\n",x+1e-9);return 0;}
```

## 7638 · 三角形的面积

```c
#include <stdio.h>
#include <math.h>
int main(){double a,b,c,p,s;scanf("%lf%lf%lf",&a,&b,&c);p=(a+b+c)/2.0;s=sqrt(p*(p-a)*(p-b)*(p-c));printf("area=%.2f\n",s);return 0;}
```

## 7639 · 一元二次方程的解

```c
#include <stdio.h>
#include <math.h>
int main(){double a,b,c,d,x1,x2,t;scanf("%lf%lf%lf",&a,&b,&c);d=b*b-4*a*c;x1=(-b+sqrt(d))/(2*a);x2=(-b-sqrt(d))/(2*a);if(x1<x2){t=x1;x1=x2;x2=t;}if(fabs(x1)<0.0000001)x1=0;if(fabs(x2)<0.0000001)x2=0;printf("x1=%.2f\nx2=%.2f\n",x1,x2);return 0;}
```

## 7640 · 甲流疫情死亡率

```c
#include <stdio.h>
int main(){double a,b;scanf("%lf%lf",&a,&b);printf("%.3f%%\n",b/a*100.0);return 0;}
```

## 7641 · 多项式的值

```c
#include <stdio.h>
int main(){double x,a,b,c,d;scanf("%lf%lf%lf%lf%lf",&x,&a,&b,&c,&d);printf("%.7f\n",a*x*x*x+b*x*x+c*x+d);return 0;}
```

## 7642 · 并联电阻的阻值

```c
#include <stdio.h>
int main(){double r1,r2;scanf("%lf%lf",&r1,&r2);printf("%.2f\n",1.0/(1.0/r1+1.0/r2));return 0;}
```

## 7643 · 温度转换

```c
#include <stdio.h>
int main(){double f;scanf("%lf",&f);printf("%.5f\n",5.0*(f-32.0)/9.0);return 0;}
```

## 7645 · 绝对值

```c
#include <stdio.h>
int main(){int x;scanf("%d",&x);if(x<0)x=-x;printf("%d\n",x);return 0;}
```

## 7646 · 三实数排序

```c
#include <stdio.h>
int main(){double a,b,c;int x,y,z,t;scanf("%lf%lf%lf",&a,&b,&c);x=(int)a;y=(int)b;z=(int)c;if(x>y){t=x;x=y;y=t;}if(y>z){t=y;y=z;z=t;}if(x>y){t=x;x=y;y=t;}printf("%d %d %d\n",x,y,z);return 0;}
```

## 7647 · 等级制

```c
#include <stdio.h>
int main(){double x;scanf("%lf",&x);if(x>=90)puts("A");else if(x>=80)puts("B");else if(x>=70)puts("C");else if(x>=60)puts("D");else puts("E");return 0;}
```

## 7648 · 一元二次方程的解II

```c
#include <stdio.h>
#include <math.h>
int main(){double a,b,c,d,x1,x2,t;scanf("%lf%lf%lf",&a,&b,&c);if(fabs(a)<1e-12){puts("0");return 0;}d=b*b-4*a*c;if(d<0){puts("-1");return 0;}x1=(-b+sqrt(d))/(2*a);x2=(-b-sqrt(d))/(2*a);if(x1<x2){t=x1;x1=x2;x2=t;}if(fabs(x1)<0.0000001)x1=0;if(fabs(x2)<0.0000001)x2=0;printf("x1=%.2f\nx2=%.2f\n",x1,x2);return 0;}
```

## 7649 · 闰年

```c
#include <stdio.h>
int main(){int y;scanf("%d",&y);if((y%4==0&&y%100!=0)||y%400==0)puts("Y");else puts("N");return 0;}
```

## 7650 · 五分制

```c
#include <stdio.h>
int main(){int x;scanf("%d",&x);switch(x){case 5:puts("A");break;case 4:puts("B");break;case 3:puts("C");break;case 2:puts("D");break;case 1:puts("E");break;}return 0;}
```

## 7651 · 等级划分

```c
#include <stdio.h>
#include <ctype.h>
int main(){char c;scanf(" %c",&c);switch(toupper((unsigned char)c)){case 'A':puts("Very good!");break;case 'B':puts("Good!");break;case 'C':puts("OK!");break;case 'D':puts("Bad!");break;case 'E':puts("Too bad!");break;}return 0;}
```

## 7652 · 快递费

```c
#include <stdio.h>
int main(){double p,w,s,d;int k;scanf("%lf%lf%lf",&p,&w,&s);k=(int)(s/250.0);switch(k){case 0:d=0.0;break;case 1:d=0.02;break;case 2:case 3:d=0.05;break;case 4:case 5:case 6:case 7:d=0.08;break;case 8:case 9:case 10:case 11:d=0.10;break;default:d=0.15;break;}printf("%.2f\n",p*w*s*(1.0-d));return 0;}
```

## 7653 · 第几天

```c
#include <stdio.h>
int main(){int y,m,d,days[13]={0,31,28,31,30,31,30,31,31,30,31,30,31};int i,total=0,leap;scanf("%d%d%d",&y,&m,&d);if(y<=0||m<1||m>12||d<1){puts("data error!");return 0;}leap=(y%4==0&&y%100!=0)||y%400==0;if(leap)days[2]=29;if(d>days[m]){puts("data error!");return 0;}for(i=1;i<m;i++)total+=days[i];total+=d;printf("%d\n",total);return 0;}
```

## 说明

- 提交账号：页面当前登录用户。
- 全部提交均已由课程平台编译并运行测试。
- 未修改 Believer Mod 任何代码；本文件仅为课程题解答归档。
- 题目页面属于外部课程平台，最终成绩以课程系统记录为准。
