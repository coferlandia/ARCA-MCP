

# Página 1

Manuales para el desarrollador
Facturación 
Electrónica
RG 4291 – Proyecto FE v4.1
Agencia de Recaudación y Control Aduanero
Subdirección General de Sistemas y 
Telecomunicaciones
ARCA-SDG SIT 
Revisión correspondiente al 9 de Junio de 2025


# Página 2

Historial de modificaciones
Ver Fecha Edición Descripción
0.1 08-09-2010 SDG SIT/DIF Versión inicial del documento
1.1 18-03-2011 SDG SIT/DIF
Se agrega ítem 1.5 con Dirección URL de Homologa-
ción y Producción del ws.
Se agrega el punto “Operaciones a realizar según la 
RG de aplicación.” En este se indica que métodos se 
utilizan para CAE y cuales para CAEA
Mejora en el detalle de los objetos necesarios envia-
dos en un request.
Mejora en la descripción de las validaciones de nego-
cio y exposición de los campos.
Se permite como receptor del comprobante a sujetos 
identificados como no categorizados.
En método FECAESolicitar y FECAEARegInformativo: 
se corrigen leyendas sobre códigos de error, se agre-
gan nuevas validaciones. 
Se agrega lógica a los datos informados sobre el ele-
mento “Opcionales” de los métodos FECAESolicitar y 
FECAEARegInformativo.
Se corrige precisión en las definiciones de los atributos 
enviados en los request y response.
Se mejora la descripción de la operatoria ante errores 
y se agregan ejemplos para FECAEARegInformativo.
Con el fin de guiar al desarrollador sobre los cambios 
impactados en el presente release se adjunta ANEXO 
1 a modo descriptivo sobre los códigos de errores que 
el nuevo relese soporta.



# Página 3

2.0 15-04-2011 SDG SIT/DIF
Se agrega funcionalidad y se adaptan los métodos pú-
blicos con el fin de soportar comprobantes tipo “C” se-
gún Resolución General AFIP Nº 3067/2011. 
A quienes tengan que desarrollar el cliente consumidor 
del servicio para informar comprobantes tipo “C” con-
sultar  ANEXO 2.
Error en la documentación del Relese 1.1. Se mueve el 
código 1413 de la grilla de excluyentes a no excluyen-
tes.
2.1 22-07-2013 SDG SIT/DIF
Comprobante Bienes Usados
Se agrega validación sobre el código 10000, empadro-
namiento en bienes usados.
Se agregan los código 10075, 10076, 10077, 10078, 
10079, 10080, 10081, 10082, 10083, 10084, 10085. 
Se modifican los código 10007, 10012, 10015, 10043, 
10044, 10045, 10047, 10048, 10068
2.2 03-10-2014 SDG SIT/DIF
Comprobante Bienes Usados
Se reemplazan los códigos Opcionales 51, 52 y 53 por 
91, 92 y 93 respectivamente.
Por el cambio de códigos Opcionales se modifican las 
descripciones de las validaciones de los códigos 
10077, 10078 ,10079 ,10080 ,10081 ,10082 ,10083 
,10084, 10085
2.3 09-10-2014 SDG SIT/DIF
Se adaptan los métodos públicos con el fin de incorpo-
rar las alícuotas identificadas en la Ley 26982. 
2.4 22-10-2014 SDG SIT/DIF
Se adaptan los métodos públicos con el fin de incorpo-
rar mediante códigos Opcionales lo expuesto en la Re-
solución General 3668
Se agregan los códigos: 10086, 10087, 10088, 10089, 
10090, 10091, 10092, 10093, 10094, 10095 
2.5 01-04-2014 SDG SIT/DIF
Se adaptan los métodos públicos con el fin de incorpo-
rar la autorización de comprobantes C a los contribu-
yentes exentos en el impuesto.
Se agregan los códigos 10096



# Página 4

2.6 01-07-2015 SDG SIT/DIF
Se adaptan los métodos públicos con el fin de incorpo-
rar los comprobantes tipo M y los códigos de opciona-
les según  la Resolución General 3749.
Se modifican los códigos 10066 y 1100.
Para comprobantes M se modificaron los sig. 4ódigos:
10000, 10007, 10012, 10013, 10017, 10019, 10020, 
10021, 10040, 10051, 10061, 10063, 10068
Para dar soporte a RG3749 se dan de alta los sig. có-
digos:
10097, 10098, 10099, 10110, 10111, 10112, 10113, 10114, 
10115
Se agrega la descripción de la validación 1427.
Se agrega la validación 1428.
Se agrega información sobre canales de atención y sitios de 
consultas
2.7 01-01-2016 SDG SIT/DIF
Código 10017. Por error de documentación se en-
contraba dentro del grupo de las validaciones exclu-
yentes. 
Para dar soporte a RG3779 se dan de alta los sig. có-
digos:
10116, 10117, 10118
2.8 01-09-2016 SDG SIT/DIF
Se agrega estructura <Observaciones> dentro de <FE-
CAEASolicitarResult><ResultGet> para soportar códi-
gos de observación al momento de solicitar un CAEA. 
Alta del código 15100 como validación no excluyente 
al momento de solicitar un CAEA.
Alta de los códigos 1429, 1430, 1431 como validacio-
nes no excluyentes al momento de informar un com-
probante.
Baja del código 1428, 15013
2.9 13-03-2017 SDG SIT/DIF
Se modifican los códigos 10040, 806, 807
Se dan de alta los códigos 10119, 10120, 10121, 
10122, 808, 809, 810 y 811 comprobantes asociados 
88, 991.
Para dar soporte a la RG 4004-E se dan de alta los 
sig. códigos: 10123, 10124, 10125, 10126, 10127, 
10128, 10129, 10130, 10131, 10132 y se modifican los 
sig. 10040, 10054, 10112



# Página 5

2.10 09/08/2017 SDG SIT/DIF
Se agrega estructura de Compradores para dar sopor-
te a receptores múltiples. Los códigos para dar soporte 
a esta nueva estructura van de 10133 al 10150
Se agrega en el manual la validación correspondiente 
al código 10119 y 10070.
Se da de alta el código 11002.
Se agrega la validación 10151 para cuando envía cuit 
en comprobantes asociados.
Se agrega la validación 1432 con el fin de limitar el in-
greso de compradores múltiples por régimen informati-
vo de CAEA.
2.11 01/08/2018 SDG SIT/DIF
Se habilita autorización de comprobantes C mediante 
la modalidad CAEA según RG 4291.
Se modifica el período de solicitud del CAEA.  Se agre-
ga opción para solicitarlo dentro de cada quincena. 
Se modifican las validaciones de los códigos 1417, 
1418, 1419, 1422, 10014, 10015 con el fin de dar so-
porte a todos los comprobantes B > a $10000 según 
RG 4291. La validación de $10000, será la resultante 
de importe total * cotización. En caso de mon_id = PES 
la cotización siempre es =1.
Se modifica el código 700, se agregan recibos tipo A, 
B, C y M
Se modifica el código 1404 se agregan comprobantes 
tipos B y C
Se modifica el código 1405, se agregan comprobantes 
tipo C
Baja de los códigos 1430, 15100
Se modifican las validaciones 15006, 15010
Se agregan validaciones desde el código 1433 al 
1445. 
Se agregan las sig. validaciones 
10152,10071,15015,15014.
Se da de baja la validación 15007



# Página 6

2.12
01/10/2018 SDG SIT/DIF
Se amplía el campo punto de venta de numérico de 4 
a numérico de 5 a partir del dia 01/10/2018.
Se modifica estructura del mensaje de respuesta del méto-
do público FEParamGetPtosVenta, atributo PtoVen-
ta.<soap:Body><FEParamGetPtosVentaResponse><FEPara-
mGetPtosVentaResult><ResultGet><PtoVenta><Nro> de 
short a int.
Se modifica la estructura general de los mensajes de 
respuesta incluyendo información adicional al header 
del response (ver punto 1.4).
Se modifican las validaciones de los sig. Códigos 1206, 1300, 
10004, 10101, 11000. 
Se modifica la validación 10119, pasa de 50% a un 
20%.
2.13 – 
Beta 1
01/11/2018 SDG SIT/DIF
Se agregan comprobantes “Factura de Crédito”
Se modifican los sig. códigos para modalidad de auto-
rización CAE: 
10000, 10003, 10007, 10015, 10016, 10040, 10042, 
10068, , 10151, 10152
Se dan de alta los sig. códigos para modalidad de au-
torización CAE:
10153, 10154, 10155, 10156, 10157, 10158, 10159, 
10160, 10161, 10162, 10163, 10164, 10165, 10166, 
10167, 10168, 10169, 10170, 10171, 10172, 10173, 
10174, 10175, 10176, 10177, 10178, 10179, 10180, 
10181, 10182, 10183, 10184, 10185
Se modifican los sig. códigos para modalidad de auto-
rización CAEA: 
Se dan de alta los sig. códigos para modalidad de au-
torización CAEA:



# Página 7

2.13 – 
Beta 2 
16/01/2019 SDG SIT/DIF
Se agregan declaraciones sobre el campo CbteFch, 
FchVtoPago
Se agregan observaciones sobre el campo CbtesAso-
c.CbteFch
Se da baja la validación 10000 orden 12, 10179, 10182
Se modifica validación 10000 orden 4
Se modifica la validación  10016, 10040, 10184, 10185
Se dan de alta los códigos 10186, 10187, 10188
2.13 11/03/2019 SDG SIT/DIF
Se agrega referencia comercial para dar soporte a fac-
turas de crédito mediante campo en estructura <Opcio-
nales> 
Se dan de alta los códigos 10189, 10190, 10191, 
10192
Se modifican las validaciones 10154, 10169, 10170
2.14 01/05/2019 SDG SIT/DIF
Se da de alta la validación 10193
Se da de baja la validación 10185
Se modifican las validaciones de los códigos 1415, 
1417, 1418, 1419, 1422, 10014, 10015 con el fin de 
dar soporte a todos los comprobantes B > a $10000 
según Resolución General 4444/2019. La validación 
de $10000, será la resultante de importe total * cotiza-
ción. En caso de mon_id = PES la cotización siempre 
es =1.
2.15 01/07/2019 SDG SIT/DIF
Se da de alta el código 10194 para modalidad de auto-
rización CAE.
Se modifican los sig. códigos para modalidad de auto-
rización CAEA: 10003, 700, 711, 807, 808
Se dan de alta los sig. códigos para modalidad de au-
torización CAEA:  812, 813, 1446, 1450, 1451, 1452, 
1453, 1454, 1455, 1456, 1457, 1458, 1459, 1460, 
1461, 1462, 1463, 1464, 1465, 1466, 1467, 1468, 
1469, 1470, 1471, 1472, 1474, 1475, 1476, 1477, 
1478, 1479, 1480, 1481, 1482, 1483, 1486, 1487, 1488
2.16 01/11/2019 SDG SIT/DIF
Se da de alta el código 10195 para modalidad de auto-
rización CAE.
Se da de alta el código 1489 para modalidad de autori-
zación CAEA. 



# Página 8

2.17 26/02/2020 SDG SIT/DIF
Se modifica validación sobre los topes de facturación 
con el fin de aplicar las adecuaciones indicadas en la 
Resolución General AFIP N° 4444/2019.
Los códigos que se modifican son: 1415, 1417, 1418, 
1419, 1422, 10014, 10015. 
2.18 – 
Beta 1
01/05/2020 SDG SIT/DIF
ADECUACIONES  RESOLUCIÓN GENERAL N° 
4.540/2019
Se agrega estructura PeriodoAsoc para los métodos 
FECAEARegInformativo, FECAESolicitar, FEComp-
Consultar.
Para modalidad de autorización con CAE, se dan de 
alta los códigos del 10196 al 10199 y del 10203 al 
10213
Para la modalidad de autorización con CAEA, se dan 
de alta los códigos del 1490 al 1504 
2.18 18/06/2020 SDG SIT/DIF
ADECUACIONES  RESOLUCIÓN GENERAL N° 
4.540/2019
Para CAE, código 10209 pasa de rechazar a observar.
Para CAEA, código 1500 pasa de rechazar a observar.
2.19 19/10/2020 SDG SIT/DIF
Adecuación para que el campo opcional 23 – Referen-
cia Comercial deje de ser exclusivo para Facturas de 
Crédito, ahora se puede usar para el resto de los com-
probantes habilitados. Para CAE se modifica valida-
ción del código 10169, 10189. Para CAEA se modifica 
validación del código 1466, 1482 
Para la modalidad de autorización CAEA, se adaptan 
los métodos públicos con el fin de incorporar mediante 
códigos Opcionales lo expuesto en la Resolución Ge-
neral 3668. Se agregan los códigos: 1505, 1506, 1507, 
1508, 1509, 1510, 1511, 1512
Se dan de baja los códigos 10087, 10093
Para la modalidad CAE el código 10195 pasa de ob-
servar a rechazar cuando el receptor del comprobante 
se encuentra en condición de apócrifo a la fecha de 
autorización del comprobante.



# Página 9

2.20 29/01/2021 SDG SIT/DIF
Para la modalidad de autorización CAE y CAEA, se adaptan 
los métodos públicos con el fin de incorporar mediante 
códigos Opcionales y solo para las facturas de crédito el 
identificar que representa si se transfiere al sistema de 
circulación abierta o al agente de depósito colectivo.
Para CAE
Se modifican los códigos 10169, 10170 y 10172
Se dan de alta los códigos 10214, 10215 y 10216
Para CAEA
Se modifican los códigos 1466, 1467, 1469
Se dan de alta los códigos 1513, 1514, 1515
2.21 01/06/2021 SDG SIT/DIF
ADECUACIONES LEY 27.618 “REGIMEN DE SOSTENIMIENTO 
E INCLUSIÓN FISCAL PARA PEQUEÑOS COTRIBUYENTES”
Para CAE, se modifica el código 10063, 10149, 10177
Se da de alta el código 10217.
Para CAEA, se modifica el código 1402, 1474
Se da de alta el código 1516.
2.22 07/09/2021 SDG SIT/DIF
Para CAE 10177
Para CAEA 1474



# Página 10

3.0 – 
Beta 1
15/10/2022 SDG SIT/DIF
Según la RG 5259/2022 de remitos Cárnicos y la RG 
5264/2022 de remitos Harinero, procedemos a hacer 
las adecuaciones del servicio mediante las sig. actuali-
zaciones:
Se agrega un método público “FEParamGetActivida-
des“, que permite determinar todas las actividades vi-
gentes del emisor del comprobante.
Se agrega para CAE y para CAEA una estructura de 
actividades vinculadas al comprobante tanto en la emi-
sión de CAE, en el régimen de información de CAEA y 
en las consultas de los comprobantes ya autorizados.
Se agregan una lista de códigos con validaciones con 
el objetivo de controlar las actividades y los remitos 
asociados según corresponda. Ver Anexo
Para CAE, se dan de alta los sig. códigos
10218, 10219, 10220, 10221, 10222, 10223, 10224, 
10225, 10226, 10227, 10228, 10229, 10230, 10231, 
10232
De obtenerse los sig. códigos de validación: 
10225,10226,10227,10228,10229,10230,10231 
Si la actividad es Cárnica y la fecha de emisión se en-
cuentra comprendida entre el 15/11/2022 y el 
14/12/2022 los códigos SOLO observan el comproban-
te. Con fecha igual o posterior al 15/12/2022 los com-
probantes pasan a quedar rechazados.
Si la actividad es Harina y la fecha de emisión se en-
cuentra comprendida entre el 01/02/2023 y el 
28/02/2023 los códigos SOLO observan el comproban-
te. Con fecha igual o posterior al 01/03/2023 los com-
probantes pasan a quedar rechazados.
Para CAEA, se dan de alta los sig. códigos
16000, 16001, 16002, 16003, 16004, 16005, 16006, 
16007, 16008, 16009, 16010, 16011, 16012, 16013, 
16014
Se modifican los siguientes códigos: Para CAE: 10120, 
10121, 10122. Para CAEA: 806, 809, 810, 811



# Página 11

3.0 01/12/2022 SDG SIT/DIF
Según la RG 5259/2022 de remitos Cárnicos y la RG 
5264/2022 de remitos Harinero, procedemos a hacer 
las adecuaciones del servicio mediante las sig. actuali-
zaciones:
Se agrega un método público “FEParamGetActivida-
des“, que permite determinar todas las actividades vi-
gentes del emisor del comprobante.
Se agrega para CAE y para CAEA una estructura de 
actividades vinculadas al comprobante tanto en la emi-
sión de CAE, en el régimen de información de CAEA y 
en las consultas de los comprobantes ya autorizados.
Se agregan una lista de códigos con validaciones con 
el objetivo de controlar las actividades y los remitos 
asociados según corresponda. Ver Anexo
Para CAE, se dan de alta los sig. códigos
10218, 10219, 10220, 10221, 10222, 10223, 10224, 
10225, 10226, 10227, 10228, 10229, 10230, 10231, 
10232
De obtenerse los sig. códigos de validación: 
10225,10226,10227,10228,10229,10230,10231 
Si la actividad es Cárnica y la fecha de emisión se en-
cuentra comprendida entre el 15/11/2022 y el 
14/12/2022 los códigos SOLO observan el comproban-
te. Con fecha igual o posterior al 15/12/2022 los com-
probantes pasan a quedar rechazados.
Si la actividad es Harina y la fecha de emisión se en-
cuentra comprendida entre el 01/02/2023 y el 
28/02/2023 los códigos SOLO observan el comproban-
te. Con fecha igual o posterior al 01/03/2023 los com-
probantes pasan a quedar rechazados.
Para CAEA, se dan de alta los sig. códigos
16000, 16001, 16002, 16003, 16004, 16005, 16006, 
16007, 16008, 16009, 16010, 16011, 16012, 16013, 
16014
Se modifican los siguientes códigos: Para CAE: 10120, 
10121, 10122. Para CAEA: 806, 809, 810, 811 
3.1 01/01/2023 SDG SIT/DIF
Se modifica validación 10119. El tipo de cambio no po-
drá ser inferior al 20% ni superior en un 200% del que 
suministra ARCA como orientativo de acuerdo a la co-
tización oficial.



# Página 12

3.2 12/12/2023 SDG SIT/DIF
Se modifica validación 10119. El tipo de cambio no po-
drá superior en un 400% del que suministra ARCA 
como orientativo de acuerdo a la cotización oficial.
3.3 04/01/2024 SDG SIT/DIF
Se modifica validación 10119. El tipo de cambio no po-
drá ser inferior al 2% ni superior en un 400% del que 
suministra ARCA como orientativo de acuerdo a la co-
tización oficial.
Adecuaciones en el sistema de alertas y monitoreo en 
la emisión de comprobantes.
Para CAE, se dan de alta los siguientes códigos: 
10234, 10235, 10236
Para CAEA, se dan de alta los siguientes códigos: 814, 
815, 816, 817
3.4 17/05/2024 SDG SIT/DIF
Adecuaciones en el sistema de alertas y monitoreo en 
la emisión de comprobantes.
Para CAE, se dan de alta los siguientes códigos: 
10237, 10238
Para CAEA, se dan de alta los siguientes códigos: 818, 
819



# Página 13

4.0 17/03/2025 Se agregan los siguientes campos nuevos:
<CanMisMonExt>
<CondicionIVAReceptorId>
Asimismo se agregan los siguientes códigos de erro-
res/observaciones:
Para CAE, se dan de alta los siguientes códigos: 
10239, 10240, 10241,10242, 10243, 10245, 10246
Para CAEA, se dan de alta los siguientes códigos: 820, 
821, 822, 823, 824, 825, 826
Se modifica el código de error: para CAE 10038 y para 
CAEA 726 correspondiente al campo MonCotiz. Si se 
indica que el pago del comprobante se realiza en la 
misma moneda extranjera que la factura, la cotización 
de la moneda provista debe coincidir exactamente con 
la registrada en las bases de ARCA para el día hábil 
anterior a la fecha de emisión del comprobante, si esta 
es anterior a la fecha actual, o bien con la registrada 
para el día hábil anterior a la fecha actual, si la fecha 
de emisión es posterior a esta. En caso contrario, se 
puede omitir el campo de Cotización de Moneda.
Se modifica el método “FEParamGetCotizacion”, agre-
gando de forma opcional la fecha de la cotización a 
querer consultar. Se agrega el código de error 12002 
para validar el nuevo campo.
También se agrega nuevo método "FEParamGetCon-
dicionIvaReceptor" para recuperar los valores de refe-
rencia de los códigos correspondientes a la condición 
de IVA del receptor. Se agrega el código de error 
10244 en la validación de dicho método. 
A partir del 6 de abril de 2025 podrá enviarse de forma 
opcional el campo Condición Frente al IVA del recep-
tor, hasta tanto entre en vigencia su obligatoriedad re-
glamentada por la Resolución General N°5616, en 
cuyo momento pasará a rechazar la emisión de com-
probantes sin este dato.



# Página 14

4.1 09/06/2025 SDG SIT/DIF
Será obligatorio el campo Condición Frente al IVA del 
receptor, atento a la entrada en vigencia reglamentada 
por la Resolución General N°5616. Por tal motivo los 
códigos de error 10245 para CAE y 825 para CAEA 
quedaran en desuso.



# Página 15

Contenido
INTRODUCCIÓN.................................................................................................................................................17
ALCANCE.........................................................................................................................................................17
AUTENTICACIÓN...............................................................................................................................................17
ESTRUCTURA GENERAL DEL MENSAJE DE RESPUESTA (RESPONSE)...............................................................................17
TRATAMIENTO DE ERRORES EN EL WS...................................................................................................................18
TRATAMIENTO DE EVENTOS.................................................................................................................................19
DIRECCIÓN URL...............................................................................................................................................20
CANALES DE ATENCIÓN......................................................................................................................................20
SITIOS DE CONSULTA..........................................................................................................................................20
WS DE NEGOCIO..............................................................................................................................................22
OPERACIONES..................................................................................................................................................22
MÉTODO DE AUTORIZACIÓN DE COMPROBANTES ELECTRÓNICOS POR CAE (FECAESOLICITAR).......................................23
 DIRECCIÓN URL (HOMOLOGACIÓN)....................................................................................................................23
MENSAJE DE SOLICITUD.....................................................................................................................................23
MENSAJE DE RESPUESTA....................................................................................................................................34
VALIDACIONES Y ERRORES..................................................................................................................................37
OPERATORIA ANTE ERRORES...............................................................................................................................71
EJEMPLOS........................................................................................................................................................72
MÉTODO DE OBTENCIÓN DE CAEA (FECAEASOLICITAR).........................................................................................85
DIRECCIÓN URL (HOMOLOGACIÓN)....................................................................................................................85
MENSAJE DE SOLICITUD.....................................................................................................................................85
MENSAJE DE RESPUESTA....................................................................................................................................86
VALIDACIONES Y ERRORES..................................................................................................................................88
EJEMPLO SIN OBSERVACIONES:...........................................................................................................................89
EJEMPLO CON OBSERVACIONES:..........................................................................................................................90
MÉTODO DE CONSULTA DE CAEA (FECAEACONSULTAR)........................................................................................91
DIRECCIÓN URL (HOMOLOGACIÓN)....................................................................................................................91
MENSAJE DE SOLICITUD.....................................................................................................................................91
MENSAJE DE RESPUESTA....................................................................................................................................92
VALIDACIONES, ACCIONES Y ERRORES...................................................................................................................94
EJEMPLO:........................................................................................................................................................94
RECUPERADOR DE VALORES REFERENCIALES DE CÓDIGOS DE TIPOS DE COMPROBANTE (FEPARAMGETTIPOSCBTE).............95
DIRECCIÓN URL (HOMOLOGACIÓN)....................................................................................................................95
MENSAJE DE SOLICITUD.....................................................................................................................................95
MENSAJE DE RESPUESTA....................................................................................................................................96
RECUPERADOR DE VALORES REFERENCIALES DE CÓDIGOS DE TIPOS DE CONCEPTOS (FEPARAMGETTIPOSCONCEPTO)......... 97
ESTE MÉTODO DEVUELVE LOS TIPOS DE CONCEPTOS POSIBLES EN ESTE WS.................................................................97
DIRECCIÓN URL (HOMOLOGACIÓN)....................................................................................................................97
MENSAJE DE SOLICITUD.....................................................................................................................................97
MENSAJE DE RESPUESTA....................................................................................................................................98
RECUPERADOR DE VALORES REFERENCIALES DE CÓDIGOS DE TIPOS DE DOCUMENTOS (FEPARAMGETTIPOSDOC).............. 99
DIRECCIÓN URL (HOMOLOGACIÓN)....................................................................................................................99
MENSAJE DE SOLICITUD.....................................................................................................................................99
MENSAJE DE RESPUESTA..................................................................................................................................100
RECUPERADOR DE VALORES REFERENCIALES DE CÓDIGOS DE TIPOS DE ALÍCUOTAS (FEPARAMGETTIPOSIVA)...................101
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................101
MENSAJE DE SOLICITUD...................................................................................................................................102
MENSAJE DE RESPUESTA..................................................................................................................................102
RECUPERADOR DE VALORES REFERENCIALES DE CÓDIGOS DE TIPOS DE MONEDAS (FEPARAMGETTIPOSMONEDAS)..........103
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................104
MENSAJE DE SOLICITUD...................................................................................................................................104
MENSAJE DE RESPUESTA..................................................................................................................................104



# Página 16

RECUPERADOR DE VALORES REFERENCIALES DE CÓDIGOS DE TIPOS DE DATOS OPCIONALES (FEPARAMGETTIPOSOPCIONAL)
....................................................................................................................................................................106
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................106
MENSAJE DE SOLICITUD...................................................................................................................................106
MENSAJE DE RESPUESTA..................................................................................................................................106
RECUPERADOR DE VALORES REFERENCIALES DE CÓDIGOS DE TIPOS DE TRIBUTOS (FEPARAMGETTIPOSTRIBUTOS)............108
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................108
MENSAJE DE SOLICITUD...................................................................................................................................108
MENSAJE DE RESPUESTA..................................................................................................................................109
RECUPERADOR DE LOS PUNTOS DE VENTA ASIGNADOS A FACTURACIÓN ELECTRÓNICA QUE SOPORTEN CAE Y CAEA VÍA WEB 
SERVICES (FEPARAMGETPTOSVENTA)................................................................................................................110
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................110
MENSAJE DE SOLICITUD...................................................................................................................................110
MENSAJE DE RESPUESTA..................................................................................................................................111
RECUPERADOR DE COTIZACIÓN DE MONEDA (FEPARAMGETCOTIZACION).................................................................112
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................113
MENSAJE DE SOLICITUD...................................................................................................................................113
MENSAJE DE RESPUESTA..................................................................................................................................113
VALIDACIONES Y ERRORES................................................................................................................................115
MÉTODO PARA INFORMAR CAEA SIN MOVIMIENTO (FECAEASINMOVIMIENTOINFORMAR)........................................115
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................116
MENSAJE DE SOLICITUD...................................................................................................................................116
MENSAJE DE RESPUESTA..................................................................................................................................116
VALIDACIONES Y ERRORES................................................................................................................................118
MÉTODO DUMMY PARA VERIFICACIÓN DE FUNCIONAMIENTO DE INFRAESTRUCTURA (FEDUMMY)................................118
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................118
MENSAJE DE SOLICITUD...................................................................................................................................119
MENSAJE DE RESPUESTA..................................................................................................................................119
RECUPERADOR DE ULTIMO VALOR DE COMPROBANTE REGISTRADO (FECOMPULTIMOAUTORIZADO).............................. 119
RETORNA EL ULTIMO COMPROBANTE AUTORIZADO PARA EL TIPO DE COMPROBANTE / CUIT / PUNTO DE VENTA INGRESADO / 
TIPO DE EMISIÓN............................................................................................................................................119
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................120
MENSAJE DE SOLICITUD...................................................................................................................................120
MENSAJE DE RESPUESTA..................................................................................................................................120
VALIDACIONES, ACCIONES Y ERRORES.................................................................................................................122
RECUPERADOR DE CANTIDAD MÁXIMA DE REGISTROS FECAESOLICITAR / FECAEAREGINFORMATIVO 
(FECOMPTOTXREQUEST).................................................................................................................................122
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................122
MENSAJE DE SOLICITUD...................................................................................................................................122
MENSAJE DE RESPUESTA..................................................................................................................................123
MÉTODO PARA INFORMAR COMPROBANTES EMITIDOS CON CAEA (FECAEAREGINFORMATIVO)..................................124
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................124
MENSAJE DE SOLICITUD...................................................................................................................................124
MENSAJE DE RESPUESTA..................................................................................................................................131
VALIDACIONES Y ERRORES................................................................................................................................133
OPERATORIA ANTE ERRORES.............................................................................................................................157
OPERATORIA ANTE ERRORES, EJEMPLOS..............................................................................................................158
MÉTODO PARA CONSULTAR CAEA SIN MOVIMIENTO (FECAEASINMOVIMIENTOCONSULTAR)......................................178
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................178
MENSAJE DE SOLICITUD...................................................................................................................................178
MENSAJE DE RESPUESTA..................................................................................................................................179
VALIDACIONES Y ERRORES................................................................................................................................181
MÉTODO PARA CONSULTAR COMPROBANTES EMITIDOS Y SU CÓDIGO (FECOMPCONSULTAR).......................................181
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................181



# Página 17

MENSAJE DE SOLICITUD...................................................................................................................................181
MENSAJE DE RESPUESTA..................................................................................................................................182
VALIDACIONES Y ERRORES................................................................................................................................185
EJEMPLO.......................................................................................................................................................185
MÉTODO PARA CONSULTAR VALORES REFERENCIALES DE CÓDIGOS DE PAÍSES (FEPARAMGETTIPOSPAISES)...................... 187
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................187
MENSAJE DE SOLICITUD...................................................................................................................................187
MENSAJE DE RESPUESTA..................................................................................................................................187
MÉTODO PARA CONSULTAR LAS ACTIVIDADES (FEPARAMGETACTIVIDADES)...............................................................188
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................188
MENSAJE DE SOLICITUD...................................................................................................................................189
MENSAJE DE RESPUESTA..................................................................................................................................189
MÉTODO PARA CONSULTAR VALORES REFERENCIALES DE LOS IDENTIFICADORES DE LA CONDICIÓN FRENTE AL IVA DEL 
RECEPTOR (FEPARAMGETCONDICIONIVARECEPTOR).............................................................................................190
DIRECCIÓN URL (HOMOLOGACIÓN)..................................................................................................................190
MENSAJE DE SOLICITUD...................................................................................................................................191
MENSAJE DE RESPUESTA..................................................................................................................................191
VALIDACIONES Y ERRORES................................................................................................................................192
MARGEN DE ERROR MEDIANTE (ERROR ABSOLUTO Y ERROR RELATIVO)...................................................................193
 ANEXO..............................................................................................................................................194
CONDICIÓN FRENTE AL IVA DEL RECEPTOR..........................................................................................................195



# Página 18

Introducción 
Este documento está dirigido a quienes tengan que desarrollar el software cliente consumidor 
de los WebServices correspondientes al servicio de Facturación Electrónica  - RG 4291.
Alcance
Este documento brinda las especificaciones técnicas para desarrollar el cliente de WebServices 
para usar el Wsfev1. Debe complementarse con los documentos relativos a: “Servicio de 
Autenticación y Autorización y Establecimiento del canal de comunicación” y Resoluciones 
Generales involucradas.
Autenticación
Para utilizar cualquiera de los métodos disponibles en el presente WS es necesario un 
Ticket de Acceso provisto por el WS de Autenticación y Autorización (WSAA). 
Recordar que para consumir el WS de Autenticación y Autorización WSAA es necesa-
rio obtener previamente un certificado digital desde clave fiscal y asociarlo al ws de ne-
gocio "Facturación Electrónica".
Al momento de solicitar un Ticket de Acceso por medio del WS de Autenticación y Au-
torización  WSAA tener en cuenta que debe enviar el tag service con el valor "wsfe" y 
que la duración del mismo es de 12 hs. 
Para más información deberá redirigirse a los manuales www.arca.gob.ar/ws.
Estructura general del mensaje de Respuesta (response)
Los mensajes de respuesta que se transmiten tienen implementado el subelemento 
FEHeaderInfo contenido en el elemento opcional Header, que se contempla en la estructura 
SOAP. En este webservice se utiliza para brindar información contextual relacionada con el 
proceso del mensaje. El procesamiento de dicha información no es obligatoria en los 
respectivos clientes, pero contribuye con información contextual de procesamiento que es de 
utilidad ante posibles eventualidades.
Ejemplo de mensaje de respuesta en el ambiente de Testing
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xmlns:xsd="http://www.w3.org/2001/XMLSchema">
   <soap:Header>
      <FEHeaderInfo xmlns="http://ar.gov.afip.dif.FEV1/">
         <ambiente>Homologacion - Clo</ambiente>



# Página 19

         <fecha>2018-09-13T12:43:28.2091828-03:00</fecha>
         <id>1.0.2.0</id>
      </FEHeaderInfo>
   </soap:Header>
   <soap:Body>
   </soap:Body>
</soap:Envelope>
Ejemplo de mensaje de respuesta en el ambiente de Producción
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xmlns:xsd="http://www.w3.org/2001/XMLSchema">
   <soap:Header>
      <FEHeaderInfo xmlns="http://ar.gov.afip.dif.FEV1/">
         <ambiente>Produccion - Pto</ambiente>
         <fecha>2018-09-13T12:43:28.2091828-03:00</fecha>
         <id>1.0.2.0</id>
      </FEHeaderInfo>
   </soap:Header>
   <soap:Body>
   </soap:Body>
</soap:Envelope>
Tratamiento de errores en el WS
El tratamiento de errores en todos los servicios se realizará de la siguiente manera:
  <Errors>
        <Err>
          <Code>int</Code>
          <Msg>string</Msg>
        </Err>
        <Err>
          <Code>int</Code>
          <Msg>string</Msg>
                </Err>
  </Errors>



# Página 20

Dónde:
Campo Detalle Obligatorio
Errors Array de objeto. Err Información correspondiente a errores N
Code Código de error S
Msg Mensaje descriptivo del   error S
Para errores internos de infraestructura, los errores se devuelven en la misma estructura 
(Errors). Los códigos de error son:
Código de error Causa
500 Error interno de aplicación.
501 Error interno de base de datos.
502 Error interno de base de datos -  Autorizador CAE / Régimen CAEA – 
Transacción Activa
600 No se corresponden  token y firma.  Usuario no autorizado  a realizar esta  
operación
601 CUIT  representada no incluida en  token.
602 No existen datos en nuestros registros.
Tratamiento de eventos
El tratamiento de eventos en todos los servicios se realizará de la siguiente manera:
  <Events>
        <Evt>
          <Code>int</Code>
          <Msg>string</Msg>
        </Evt>
        <Evt>
          <Code>int</Code>
          <Msg>string</Msg>
                </Evt>
  </Events>
Dónde:



# Página 21

Campo Detalle Obligatorio
Events Array de objeto. Evt Información correspondiente al 
mensaje N
Code Código de evento S
Msg Detalla el evento que se desea comunicar S
Dirección URL
Este servicio se llama en Homologación desde:
https://wswhomo.afip.gov.ar/wsfev1/service.asmx
Para visualizar el WSDL en Homologación:
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?WSDL
Este servicio se llama en Producción desde:
https://servicios1.afip.gov.ar/wsfev1/service.asmx
Para visualizar el WSDL en Producción:
https://servicios1.afip.gov.ar/wsfev1/service.asmx?WSDL
Canales de Atención
Consultas sobre el ambiente de homologación:
- Acerca de certificados y accesos, consultar sitio http://www.arca.gob.ar/ws/
- Sobre aspectos funcionales del Web Services: wsfev1@arca.gov.ar 
Consultas sobre el ambiente de producción:
-  sri@arca.gov.ar
Consultas sobre normativa:
- facturaelectronica@arca.gov.ar
Sitios de Consulta
Biblioteca Electrónica



# Página 22

ABC – Consultas y Respuestas Frecuentes sobre: 
- Funcionalidades del WS 
- Normativa, Aplicativos y Sistemas. Opción Facturación y Registración 
Documentación   de Ayuda  
- http://www.afip.gob.ar/fe/ayuda.asp  .



# Página 23

WS de Negocio
Operaciones
2.4.1 Operaciones a realizar según la RG de aplicación.
 - Para “CAE – RG 4291” aplican los siguientes métodos:
 Método  de autorización de comprobantes electrónicos por  CAE (FECAESolicitar)
1 Para “CAEA – RG 4291” aplican los siguientes métodos:
 Método  de obtención de CAEA (FECAEASolicitar)
 Método  de consulta de CAEA (FECAEAConsultar)
 Método  para informar CAEA sin movimiento (FECAEASinMovimientoInformar)
 Método para informar comprobantes emitidos con  CAEA (FECAEARegInformativo)
 Método  para consultar CAEA sin movimiento (FECAEASinMovimientoConsultar)
2 Para ambos:
 Recuperador de valores referenciales de códigos de Tipos de comprobante 
(FEParamGetTiposCbte)
 Recuperador de valores referenciales de códigos de Tipos de Conceptos   
(FEParamGetTiposConcepto)
 Recuperador de valores referenciales de códigos de Tipos de Documentos  
(FEParamGetTiposDoc)
 Recuperador de valores referenciales de códigos de Tipos de Alícuotas 
(FEParamGetTiposIva)
 Recuperador de valores referenciales de códigos de Tipos de Monedas 
(FEParamGetTiposMonedas)
 Recuperador de valores referenciales de códigos de Tipos de datos Opcionales 
(FEParamGetTiposOpcional)
  Recuperador de valores referenciales de códigos de Tipos de Tributos 
(FEParamGetTiposTributos)
 Recuperador de los puntos de venta asignados a Facturación Electrónica que 
soporten CAE y CAEA vía Web Services (FEParamGetPtosVenta)
 Recuperador de cotización de moneda (FEParamGetCotizacion)
 Método Dummy para verificación de funcionamiento de infraestructura 
(FEDummy)
 Recuperador de ultimo valor de comprobante registrado 
(FECompUltimoAutorizado)



# Página 24

 Recuperador de cantidad máxima de registros FECAESolicitar / 
FECAEARegInformativo  (FECompTotXRequest)
 Método  para consultar Comprobantes Emitidos y su código (FECompConsultar)
 Método  para consultar las actividades vigentes del emisor 
(FEParamGetActividades)
Un contribuyente sólo necesita implementar un cliente para los métodos del WS 
correspondientes a la RG por la cual esté alcanzado. Por ejemplo, si optó por CAEA no es 
necesario que implemente soporte para los métodos FEParamGetPtosVenta.
Método  de autorización de comprobantes electrónicos por  CAE (FECAESolicitar)
El cliente envía la información del comprobante/lote que desea autorizar mediante un 
requerimiento el cual es atendido por WSFEv1 pudiendo producirse las siguientes situaciones: 
 Supere todas las validaciones, el comprobante es aprobado, se asigna el CAE y su 
respectiva fecha de vencimiento, 
 No supera alguna de las validaciones no excluyentes, el comprobante es aprobado 
con observaciones, se le asigna el CAE con la fecha de vencimiento,
 No supere alguna de las validaciones excluyentes, el comprobante no es aprobado 
y la solicitud es rechazada.
Cabe aclarar que las validaciones excluyentes son aquellas que en el caso de no ser superadas 
provocan un rechazo y las validaciones no excluyentes aprueban la solicitud pero con 
observaciones.
 Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= FECAESolicitar
Mensaje de solicitud
Recibe la información de un comprobante o lote de comprobantes.
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
       <ar:FECAESolicitar>
       <ar:Auth> 



# Página 25

          <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
         <ar:FeCAEReq>
            <ar:FeCabReq>
               <ar:CantReg>int</ar:CantReg>
               <ar:PtoVta>int</ar:PtoVta>
               <ar:CbteTipo>int</ar:CbteTipo>
            </ar:FeCabReq>
            <ar:FeDetReq>
               <ar:FECAEDetRequest>
                  <ar:Concepto>int</ar:Concepto>
                  <ar:DocTipo>int</ar:DocTipo>
                  <ar:DocNro>long</ar:DocNro>
                  <ar:CbteDesde>long</ar:CbteDesde>
                  <ar:CbteHasta>long</ar:CbteHasta>
                  <ar:CbteFch>string</ar:CbteFch>
                  <ar:ImpTotal>double</ar:ImpTotal>
                  <ar:ImpTotConc>double</ar:ImpTotConc>
                  <ar:ImpNeto>double</ar:ImpNeto>
                  <ar:ImpOpEx>double</ar:ImpOpEx>
                  <ar:ImpTrib>double</ar:ImpTrib>
                  <ar:ImpIVA>double</ar:ImpIVA>
                  <ar:FchServDesde>string</ar:FchServDesde>
                  <ar:FchServHasta>string</ar:FchServHasta>
                  <ar:FchVtoPago>string</ar:FchVtoPago>
                  <ar:MonId>string</ar:MonId>
                  <ar:MonCotiz>double</ar:MonCotiz>
                  <ar:CanMisMonExt>string</ar:CanMisMonExt>
                  <ar:CondicionIVAReceptorId>int</ar:CondicionIVAReceptorId>
                  <ar:CbtesAsoc>
                     <ar:CbteAsoc>
                        <ar:Tipo>short</ar:Tipo>
                        <ar:PtoVta>int</ar:PtoVta>
                        <ar:Nro>Long</ar:Nro>
                        <ar:Cuit>String</ar:Cuit>
                        <ar:CbteFch>String</ar:CbteFch>
                     </ar:CbteAsoc>
                  </ar:CbtesAsoc>
                  <ar:Tributos>
                     <ar:Tributo>
                        <ar:Id>short</ar:Id>
                        <ar:Desc>string</ar:Desc>
                        <ar:BaseImp>double</ar:BaseImp>
                        <ar:Alic>double</ar:Alic>
                        <ar:Importe>double</ar:Importe>
                     </ar:Tributo>
                  </ar:Tributos>
                  <ar:Iva>
                     <ar:AlicIva>
                        <ar:Id>short</ar:Id>
                        <ar:BaseImp>double</ar:BaseImp>
                        <ar:Importe>double</ar:Importe>
                     </ar:AlicIva>
                  </ar:Iva>
                  <ar:Opcionales>
                     <ar:Opcional>



# Página 26

                        <ar:Id>string</ar:Id>
                        <ar:Valor>string</ar:Valor>
                     </ar:Opcional>
                  </ar:Opcionales>
                  <ar:Compradores>
                     <ar:Comprador>
                        <ar:DocTipo>int</ar:DocTipo>
                        <ar:DocNro>Long</ar:DocNro>
                        <ar:Porcentaje>double</ar:Porcentaje>
                     </ar:Comprador>
                  </ar:Compradores>
                  <ar:PeriodoAsoc>
                     <ar:FchDesde>string</ar:FchDesde>
                     <ar:FchHasta>string</ar:FchHasta>
                  </ar:PeriodoAsoc>
                  <ar:Actividades>
                     <ar:Actividad>
                        <ar:Id>Long</ar:Id>
                     </ar:Actividad>
                  </ar:Actividades>
               </ar:FECAEDetRequest>
            </ar:FeDetReq>
         </ar:FeCAEReq>
      </ar:FECAESolicitar>
   </soapenv:Body>
</soapenv:Envelope>
Dónde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign y Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Campo Detalle Obligatorio
FeCAEReq Información del comprobante o lote de comprobantes de 
ingreso. Contiene los datos de FeCabReq y FeDetReq S
FeCabReq Información de la cabecera del comprobante o lote de 
comprobantes de ingreso S
FeDetReq Información del detalle del comprobante o lote de 
comprobantes de ingreso S
FeCabReq: La cabecera del comprobante o lote de comprobantes de ingreso está  
compuesta por los siguientes campos:



# Página 27

Campo Tipo Detalle Obligatorio
CantReg Int (4) Cantidad de registros del detalle del 
comprobante o lote de comprobantes de ingreso S
CbteTipo Int (3)
Tipo de comprobante que se está informando.  Si 
se informa más de un comprobante, todos deben 
ser del mismo tipo.
S
PtoVta Int (5)
Punto de Venta del comprobante que se está 
informando. Si se informa más de un 
comprobante, todos deben corresponder al 
mismo punto de venta.
S
FeDetReq: El detalle del comprobante o lote de comprobantes de ingreso está compuesto por 
los siguientes campos: 
Campo Tipo Detalle Obligatorio
Concepto Int(2)
Concepto del Comprobante. Valores 
permitidos:
1 Productos
2 Servicios
3 Productos y Servicios
S
DocTipo Int (2) Código de documento identificatorio del 
comprador S
DocNro Long (11) Nro.  De identificación del comprador S
CbteDesde Long (8)
Nro. De comprobante desde
Rango 1- 99999999
S
CbteHasta Long (8)
Nro. De comprobante registrado hasta
Rango 1- 99999999
S
CbteFch String (8)
Fecha del comprobante (yyyymmdd). Para 
concepto igual a 1, la fecha de emisión del 
comprobante puede ser hasta 5 días 
anteriores o posteriores respecto de la fecha 
de generación: La misma no podrá exceder el 
mes de presentación. Si se indica Concepto 
igual a 2 ó 3 puede ser hasta 10 días 
anteriores o posteriores a la fecha de 
generación. Si no se envía la fecha del 
comprobante se asignará la fecha de proceso. 
Para comprobantes del tipo MiPyMEs (FCE) 
N



# Página 28

Campo Tipo Detalle Obligatorio
del tipo Factura, la fecha de emisión del 
comprobante debe ser desde 5 días 
anteriores y hasta 1 día posterior respecto de 
la fecha de generación. Para notas de débito y 
crédito es hasta 5 dias anteriores y tiene que 
ser posterior o igual a la fecha del 
comprobante asociado.
ImpTotal Double 
(13+2)
Importe  total  del  comprobante, Debe ser 
igual a Importe neto no gravado + Importe 
exento + Importe neto gravado + todos los 
campos de IVA  al XX% + Importe de tributos.
S
ImpTotConc Double 
(13+2)
Importe neto no gravado.
Debe ser menor o igual a Importe total y no 
puede ser menor a cero.
No puede ser mayor al Importe total de la 
operación ni menor a cero (0).
Para comprobantes tipo C debe ser igual a 
cero (0).
Para comprobantes tipo Bienes Usados – 
Emisor Monotributista este campo 
corresponde al importe subtotal.
S
ImpNeto Double 
(13+2)
Importe neto  gravado. Debe ser menor o 
igual a Importe total y no puede ser menor a 
cero. Para comprobantes tipo C este campo 
corresponde al Importe del Sub Total.
Para comprobantes tipo Bienes Usados – 
Emisor Monotributista no debe informarse o 
debe ser igual a cero (0).
S
ImpOpEx Double 
(13+2)
Importe exento. Debe ser menor o igual a 
Importe total y no puede ser menor a cero.
Para comprobantes tipo C debe ser igual a 
cero (0).
Para comprobantes tipo Bienes Usados – 
Emisor Monotributista no debe informarse o 
debe ser igual a cero (0).
S
ImpIVA Double 
(13+2)
Suma de los importes del  array de IVA.
Para comprobantes tipo C debe ser igual a 
cero (0).
Para comprobantes tipo Bienes Usados – 
Emisor Monotributista no debe informarse o 
S



# Página 29

Campo Tipo Detalle Obligatorio
debe ser igual a cero (0).
ImpTrib Double 
(13+2) Suma de los importes del  array de tributos S
FchServDesde String (8)
Fecha de inicio  del abono para el  servicio a 
facturar. Dato  obligatorio  para concepto  2 o 
3 (Servicios / Productos y Servicios). Formato  
yyyymmdd
N
FchServHasta String (8)
Fecha de fin del abono para el  servicio a 
facturar. Dato  obligatorio  para concepto  2 o 
3 (Servicios / Productos y Servicios). Formato  
yyyymmdd. FchServHasta no puede ser menor 
a FchServDesde
N
FchVtoPago String (8)
Fecha de vencimiento  del  pago servicio a 
facturar. Dato  obligatorio  para concepto  2 o 
3 (Servicios / Productos y Servicios) o Facturas 
del tipo MiPyMEs (FCE). Formato  yyyymmdd. 
Debe ser igual o posterior a la fecha del 
comprobante.
N
MonId String (3)
Código  de moneda del  comprobante. 
Consultar método  FEParamGetTiposMonedas 
para valores posibles
S
MonCotiz Double 
(4+6)
Cotización de la moneda informada. Para PES, 
pesos  argentinos  la misma debe ser 1. De 
informar el campo, el mismo no puede quedar 
vacío.
N
CanMisMonExt String (1)
Marca que identifica si el comprobante se 
cancela en misma moneda del comprobante 
(moneda extranjera). Valores posibles S o N.
N
CondicionIVARece
ptorId Int (2)
Condición Frente al IVA del receptor. 
Consultar método 
“FEParamGetCondicionIvaReceptor”
Campo Condición Frente al IVA del receptor 
resultará obligatorio conforme lo 
reglamentado por la Resolución General N° 
5616. Si el valor informado no es valido, para 
CAE rechazará y en CAEA observará. Si el valor 
no existe rechazará en ambos casos.
N
CbtesAsoc Array Array para informar los comprobantes 
asociados <CbteAsoc> N
Tributos Array Array para informar los tributos asociados a N



# Página 30

Campo Tipo Detalle Obligatorio
un comprobante <Tributo>.
IVA Array
Array para informar las alícuotas y sus 
importes asociados a un comprobante 
<AlicIva>.
Para comprobantes tipo C y Bienes Usados – 
Emisor Monotributista no debe informar el 
array.
N
Opcionales Array Array  de campos  auxiliares. Reservado  usos 
futuros <Opcional>.  Adicionales por R.G. N
Compradores Comprador Array para informar los múltiples 
compradores. N
PeriodoAsoc Periodo
Estructura compuesta por la fecha desde y la 
fecha hasta del periodo que se quiere 
identificar
N
Actividades Actividad Array para informar las actividades asociadas 
a un comprobante. N
CbtesAsoc: Detalle de los comprobantes relacionados con el comprobante que se so -
licita autorizar (array).
Campo Tipo Detalle Obligatorio
Tipo Int (3) Código de tipo de comprobante. Consul-
tar método FEParamGetTiposCbte. S
PtoVta Int (5) Punto  de venta del comprobante asocia -
do S
Nro Long (8) Numero de comprobante asociado S
Cuit String(11) Cuit emisor del comprobante asociado N
CbteFch String(8) Fecha del comprobante asociado N
Tributos: Detalle de tributos relacionados con el  comprobante que se solicita autori -
zar (array).
Campo Tipo Detalle Obligatorio
Id Int (2) Código  tributo según método   
FEParamGetTiposTributos S
Desc String (80) Descripción del  tributo. N



# Página 31

BaseImp Double (13+2) Base imponible para la determinación del  
tributo S
Alic Double (3+2) Alícuota S
Importe Double (13+2) Importe del  tributo S
IVA: Detalle de alícuotas relacionadas con el comprobante que se solicita autorizar  
(array).
Campo Tipo Detalle Obligatorio
Id Int (2) Código  de tipo de iva. Consultar método  
FEParamGetTiposIva
S
BaseImp Double (13+2) Base imponible para la determinación de la 
alícuota.
S
Importe Double (13+2) Importe S
Opcionales: Campos auxiliares (array).  Adicionales por R.G.
Los datos opcionales sólo deberán ser incluidos si el emisor pertenece al conjunto de emisores 
habilitados a informar opcionales. En ese caso podrá incluir el o los datos opcionales que 
correspondan, especificando el identificador de dato opcional de acuerdo a la situación del 
emisor. El listado de tipos de datos opcionales se puede consultar con el método 
FEParamGetTiposOpcional. 
Ejemplo: si el emisor está incluido en el “Régimen de Promoción Industrial”, deberá incluir un 
array de opcionales con un registro como el sig. 
<ar:Opcionales>
<ar:Opcional>
<ar:Id>2</ar:Id>
<ar:Valor>12345678</ar:Valor>
</ar:Opcional>
</ar:Opcionales>
Si el comprobante que intenta autorizar corresponde a Establecimientos de educación pública 
de gestión privada según Resolución General N° 3.368 deberá incluir un array de opcionales 
con registros como el siguiente ejemplo:
<ar:Opcionales>
<ar:Opcional>
<ar:Id>10</ar:Id>
<ar:Valor>1</ar:Valor>
</ar:Opcional>
<ar:Opcional>



# Página 32

<ar:Id>1011</ar:Id>
<ar:Valor>80</ar:Valor>
</ar:Opcional>
<ar:Opcional>
<ar:Id>1012</ar:Id>
<ar:Valor>30000000007</ar:Valor>
</ar:Opcional>
</ar:Opcionales>
Si el comprobante que intenta autorizar corresponde a Operaciones económicas 
vinculadas con bienes inmuebles según RG N° 2.820 deberá incluir un array de 
opcionales con un registro como el siguiente ejemplo:
<ar:Opcionales>
<ar:Opcional>
<ar:Id>11</ar:Id>
<ar:Valor>1</ar:Valor>
</ar:Opcional>
</ar:Opcionales>
Si el comprobante que intenta autorizar corresponde a Locación temporaria de 
inmuebles con fines turísticos según RG N° 3.687 deberá incluir un array de opcionales 
con un registro como el siguiente ejemplo:
<ar:Opcionales>
<ar:Opcional>
<ar:Id>12</ar:Id>
<ar:Valor>1</ar:Valor>
</ar:Opcional>
</ar:Opcionales>
Si el comprobante que intenta autorizar corresponde a Representantes de Modelos 
según RG N° 2.863 deberá incluir un array de opcionales con un registro como el 
siguiente ejemplo:



# Página 33

<ar:Opcionales>
<ar:Opcional>
<ar:Id>13</ar:Id>
<ar:Valor>1</ar:Valor>
</ar:Opcional>
</ar:Opcionales>
Si el comprobante que intenta autorizar corresponde a Agencias de publicidad según 
RG N° 2.863 deberá incluir un array de opcionales con un registro como el siguiente 
ejemplo:
<ar:Opcionales>
<ar:Opcional>
<ar:Id>14</ar:Id>
<ar:Valor>1</ar:Valor>
</ar:Opcional>
</ar:Opcionales>
Si el comprobante que intenta autorizar corresponde a Personas físicas que 
desarrollen actividad de modelaje  según RG N° 2.863 deberá incluir un array de 
opcionales con un registro como el siguiente ejemplo:
<ar:Opcionales>
<ar:Opcional>
<ar:Id>15</ar:Id>
<ar:Valor>1</ar:Valor>
</ar:Opcional>
</ar:Opcionales>
Si el comprobante que intenta autorizar es del tipo B o C con locación de inmuebles 
destino "casa-habitación" facturación directa según RG N° 4004-E deberá incluir un 
array de opcionales con un registro como el siguiente ejemplo:



# Página 34

<ar:Opcionales>
<ar:Opcional>
<ar:Id>17</ar:Id>
<ar:Valor>2</ar:Valor>
</ar:Opcional>
</ar:Opcionales>
Si el comprobante que intenta autorizar es del tipo B o C con locación de inmuebles 
destino "casa-habitación" facturación a través de intermediario según RG N° 4004-E 
deberá incluir un array de opcionales con un registro como el siguiente ejemplo:
<ar:Opcionales>
<ar:Opcional>
<ar:Id>17</ar:Id>
<ar:Valor>1</ar:Valor>
</ar:Opcional>
</ar:Opcionales>
Si el comprobante que intenta autorizar es del tipo B o C con locación de inmuebles 
destino "casa-habitación" con facturación directa con cotitulares o indirecta con los 
datos de el/los  titular/es según RG N° 4004-E deberá incluir opcionales con al menos 2 
registros como el siguiente ejemplo:
<ar:Opcionales>
<ar:Opcional>
<ar:Id>1801</ar:Id>
<ar:Valor>30000000007</ar:Valor>
</ar:Opcional>
<ar:Opcional>
<ar:Id>1802</ar:Id>
<ar:Valor>DENOMINACION EJEMPLO</ar:Valor>
</ar:Opcional>
</ar:Opcionales>



# Página 35

Campo Tipo Detalle Obligatorio
Id String(4) Código  de Opcional, consultar método 
FEParamGetTiposOpcional S
Valor String (250) Valor S
Comprador: Detalle compradores vinculados al comprobante que se solicita autorizar  
(array).
Campo Tipo Detalle Obligatorio
DocTipo Int (2) Tipo de documento del comprador S
DocNro String (80) Número de documento del comprador S
Porcentaje Double (2+2) Porcentaje de titularidad que tiene el 
comprador S
Periodo: Estructura que permite soportar un rango de fechas.
Campo Tipo Detalle Obligatorio
FchDesde String(8)
Fecha correspondiente al inicio del perio-
do de los comprobantes que se quiere 
identiricar
S
FchHasta String(8)
Fecha correspondiente al fin del periodo  
de los comprobantes que se quiere iden -
tificar
S
Actividad: Detalle de la actividad relacionada con las actividades (array) que se indi -
can en el comprobante a autorizar.
Campo Tipo Detalle Obligatorio
Id Long (6) Código  actividad según método   
FEParamGetActividades S
Mensaje de respuesta
Retorna la información del comprobante o lote de comprobantes de ingreso agregándole el 
CAE otorgado si el comprobante fue aprobado. Ante cualquier anomalía se retorna un array 
con errores detectados (Errors) o un array de observaciones según corresponda. 
<soap:Envelope xmlns:soap=”http://www.w3.org/2003/05/soap-envelope” 
xmlns:ar=”http://ar.gov.afip.dif.fev1/”>
   <soap:Header/>
   <soap:Body>



# Página 36

      <FECAESolicitarResponse>
         <FECAESolicitarResult>
            <FeCabResp>
               <Cuit>long</Cuit>
               <PtoVta>int</PtoVta>
               <CbteTipo>int</CbteTipo>
               <FchProceso>string</FchProceso>
               <CantReg>int</CantReg>
               <Resultado>string</Resultado>
               <Reproceso>string</Reproceso >
            </FeCabResp>
            <FeDetResp>
               <FEDetResponse>
                  <Concepto>int</Concepto>
                  <DocTipo>int</DocTipo>
                  <DocNro>long</DocNro>
                  <CbteDesde>long</CbteDesde>
                  <CbteHasta>long</CbteHasta>
                  <Resultado>string</Resultado>
                  <CAE>string</CAE>
                  <CbteFch>string</CbteFch>
                  <CAEFchVto>string</CAEFchVto>
                  <Obs>
                     <Observaciones>
                        <Code>int</Code>
                        <Msg>string</Msg>
                     </Observaciones>
                  </Obs>
               </FEDetResponse>
            </FeDetResp>
            <Events>
               <Evt>
                  <Code>int</Code>
                  <Msg>string</Msg>
               </Evt>
            </Events>
            <Errors>
               <Err>
                  <Code>int</Code>
                  <Msg>string</Msg>
               </Err>
            </Errors>
         </FECAESolicitarResult>
      </FECAESolicitarResponse>
   </soap:Body>
</soap:Envelope>
Dónde:
Campo Detalle Obligatorio
FECAESolicitarResult
Información del comprobante o lote de comprobantes de 
ingreso, conteniendo el CAE otorgado. Contiene los datos 
de FeCabResp y FeDetResp, Errors y Events
S
FeCabResp Información de la cabecera del comprobante o lote de 
comprobantes de ingreso S



# Página 37

FeDetResp Información del detalle del comprobante o lote de 
comprobantes de ingreso conteniendo el CAE otorgado S
Errors Información de errores detectados N
Events Información de eventos N
FeCabResp: La cabecera del comprobante o lote de comprobantes de ingreso estará 
compuesta por los siguientes campos:
Campo Tipo Detalle Obligatorio
Cuit Long (11) Cuit del contribuyente S
PtoVta Int (5) Punto  de venta S
CbteTipo Int (3) Tipo  de comprobante S
FchProceso String (14) Fecha de proceso  formato  yyyymmddhhmiss S
CantReg Int (4) Cantidad de registros del detalle del comprobante 
o lote de comprobantes de ingreso S
Resultado String (1) Resultado S
Reproceso String Campo no operativo para esta versión. N
FeDetResp: El detalle del comprobante o lote de comprobantes de ingreso estará compuesta 
por los siguientes campos: 
Campo Tipo Detalle Obligatorio
Concepto Int (2) Concepto S
DocTipo Int (2) Código de documento identificador del comprador S
DocNro Long (11) Nro.  De identificación del comprador S
CbteDesde Long (8) Nro. De comprobante desde S
CbteHasta Long (8) Nro. De comprobante registrado hasta S
CbteFch String (8) Fecha del comprobante N
Resultado String (1) Resultado S
CAE String (14) Código de autorización electrónico N
CAEFchVto String (8) Fecha de vencimiento o vencimiento de la 
autorización N
Observaciones Array Detalle de observaciones, del comprobante N



# Página 38

Observaciones: La estructura de datos Obs muestra el  detalle de observaciones para 
un comprobante determinado;   estará compuesta por los siguientes campos: 
Campo Tipo Detalle Obligatorio
Code Int (5) Código  de observación S
Msg String (255) Mensaje S
Validaciones y errores 
Controles aplicados al objeto <Auth>
Validaciones Excluyentes
Campo / 
Grupo
Código de 
error Descripción de la validación
<Cuit> 10000
Verificación de datos registrales, Inscripción en el régimen, autorización de 
emisión de comprobantes, domicilio fiscal.  Etc.
Los mensajes posibles son
01 “LA CUIT INFORMADA NO CORRESPONDE A UN RESPONSABLE 
INSCRIPTO EN EL IMPUESTO”
02 “LA CUIT INFORMADA NO SE ENCUENTRA AUTORIZADA A EMITIR 
COMPROBANTES ELECTRONICOS ORIGINALES O EL PERIODO DE INICIO 
AUTORIZADO ES POSTERIOR AL DE LA GENERACION DE LA SOLICITUD” 
03 “LA CUIT INFORMADA REGISTRA INCONVENIENTES CON EL 
DOMICILIO FISCAL”
04 “LA CUIT INFORMADA NO SE ENCUENTRA AUTORIZADA A EMITIR 
COMPROBANTES CLASE “A” O FACTURA DE CREDITO, (Esta validación no 
aplica para comprobantes tipo C)”
05       “EL CUIT INFORMADO COMO EMISOR NO SE ENCUENTRA 
REGISTRADO DE FORMA ACTIVA EN LAS BASES DE LA ADMINISTRACIÓN.” 
06 “DEBE POSEER AL MENOS UNA ACTIVAD ACTIVA.” (Esta validación 
no aplica para comprobantes tipo C”
07   “NO AUTORIZADO A EMITIR COMPROBANTES – LA CUIT INFORMADA 
NO SE ENCUENTRA AUTORIZADA A EMITIR COMPROBANTES SEGÚN RG 
3411” (Esta validación solo aplica para comprobante 49 – Bien Usado”)
08 “NO AUTORIZADO A EMITIR COMPROBANTES – LA CUIT INFORMADA 
NO CORRESPONDE A UN EXENTO EN IVA.
09 “LA CUIT INFORMADA NO SE ENCUENTRA AUTORIZADA A EMITIR 
COMPROBANTES CLASE M”
10 “LA CUIT INFORMADA NO SE ENCUENTRA REGISTRADA COMO PYME 
SEGÚN EL REGIMEN FCE”
11 “LA CUIT INFORMADA NO TIENE ACTIVO EL DOMICILIO FISCAL 



# Página 39

ELECTRONICO”
Controles aplicados al objeto <FeCabReq>
Validaciones Excluyentes
Campo / 
Grupo
Código de 
error Descripción de la validación
<CantReg> 10001 Cantidad de registros de detalle del comprobante o lote de comprobantes 
de ingreso <CantReg> debe estar comprendido entre 1 y 9998.
<CantReg> 10002
La cantidad de registros del detalle del comprobante o lote de 
comprobantes de ingreso debe ser igual a lo informado en cabecera del 
comprobante o lote de comprobantes de ingreso <CantReg>
Cantidad de 
registros 
incluidos
10003
La cantidad de registros en detalle debe ser menor igual al valor permitido. 
Consulte método FECompTotXRequest para obtener cantidad máxima de 
registros por cada requerimiento. Para comprobantes del tipo MiPyMEs 
(FCE), la cantidad habilitada es 1 comprobante por request
<PtoVta> 10004 Campo <PtoVta> debe estar comprendido  entre 1 y 99998.
<PtoVta> 10005 El punto de venta informado debe estar dado de alta y ser del tipo RECE.
<CbteTipo> 10006 Campo CbteTipo debe ser un valor numérico mayor a 0.
<CbteTipo> 10007
Campo CbteTipo sea:
- 01, 02, 03, 04, 05 ,34, 39, 60, 63, 201, 202, 203 para los clase A
- 06, 07, 08, 09, 10, 35, 40,64, 61, 206, 207, 208 para los clase B.
- 11, 12, 13, 15, 211, 212, 213 para los clase C.
- 51, 52, 53, 54 para los clase M.
- 49 para los Bienes Usados
Consultar método FEParamGetTiposCbte.
Controles aplicados al objeto <FeDetReq>
Validaciones Excluyentes
Campo / Grupo Código 
de error Descripción de la validación
<CbteDesde> 10008 Campo <CbteDesde> se encuentre 
entre 1 y 99999999.
<CbteHasta> 10010 Campo <CbteHasta>  se encuentre 
entre 1 y 99999999.
<CbteHasta> 10011 Campo <CbteHasta>  sea mayor  o  



# Página 40

Campo / Grupo Código 
de error Descripción de la validación
igual  a <CbteDesde> para 
comprobantes tipo B. Para 
comprobantes tipo C <CbteHasta> 
debe ser igual a <CbteDesde>.
<CbteTipo> / <CbteDesde> / <CbteHasta> 10012
Para comprobantes clase A, C, M, 49 
– Bienes Usados y comprobantes 
MiPyMEs (FCE) el campo CbteDesde 
debe ser igual al campo CbteHasta
<CbteTipo> / <DocTipo> 10013
Para comprobantes clase A y M el 
campo  DocTipo  tenga valor 80 
(CUIT)
<CbteTipo> / <CbteDesde> / <CbteHasta> 10014
Para comprobantes clase B y 
CbteHasta distinto a CbteDesde el 
resultado de la operación ImpTotal / 
(CbteHasta –CbteDesde +1) < monto 
en pesos resultante según RG4444.
<CbteTipo> / <DocTipo> / <DocNro> 10015
Para comprobantes tipo B en 
pedidos múltiples (CbteDesde 
distinto a CbteHasta) y el resultado 
de la operación ImpTotal / 
(CbteHasta – CbteDesde + 1 ) < 
monto en pesos resultante según 
RG4444. el campo DocTipo deberá 
ser igual a 99, el campo  DocNro 
deberá ser cero (0).
Para comprobantes tipo B en 
pedidos individuales (CbteDesde 
igual a CbteHasta) y el resultado de 
la operación ImpTotal / (CbteHasta – 
CbteDesde + 1 ) < monto en pesos 
resultante según RG4444 si el campo 
DocTipo es igual a 99, el campo 
DocNro deberá ser cero. 
Para comprobantes tipo B 
individuales (CbteDesde igual a 
CbteHasta), si el campo DocTipo es 
80, 86 u 87, deberá verificarse que el 
número consignado se encuentre en 
los padrones de ARCA. Si DocTipo es 
80 y DocNro es 23000000000 (No 
Categorizado) esta validación no se 
tendrá en cuenta.
Si el campo DocTipo es distinto de 



# Página 41

Campo / Grupo Código 
de error Descripción de la validación
80, 86 u 87, deberá verificarse que 
se ingrese uno de los valores 
devueltos por el método 
FEParamGetTiposDoc y que se 
informe el campo DocNro.
Para pedidos individuales 
(CbteDesde igual a CbteHasta) tipo B 
con montos superiores a monto en 
pesos resultante según RG4444 el 
campo DocTipo  deberá ser igual a 
algunos de los valores devueltos por 
el  método   FEParamGetTiposDoc 
excepto 99 y deberá informar el 
campo DocNro.
Para comprobantes tipo 49 – Bienes 
Usados, DocTipo  deberá ser igual a 
algunos de los valores devueltos por 
el  método   FEParamGetTiposDoc 
excepto el 99 y deberá informar el 
campo DocNro.
Para comprobantes tipo 49 – Bienes 
Usados, si DocTipo es 80, 86 u 87, 
deberá verificarse que el número 
consignado se encuentra en los 
padrones de arca.
Para comprobantes MiPyMEs (FCE) 
el documento del receptor debe ser 
80 CUIT.
<CbteDesde> / <CbteFch> 10016
El número de comprobante 
informado <CbteDesde> debe ser 
mayor en 1 al último informado para 
igual punto de venta y tipo de 
comprobante. Consultar método 
FECompUltimoAutorizado
El campo <CbteFch> podrá ser:
-  Nulo o  comprendido  en el  rango  
N-5 y N+5 siendo  N la fecha de 
envío del pedido  de autorización, 
para Concepto= 01 Productos. La 
misma no podrá exceder el mes de 
presentación.



# Página 42

Campo / Grupo Código 
de error Descripción de la validación
-  Para Concepto 02, 03 el  campo 
CbteFch puede ser nulo o  
comprendido  en el  rango  N-10 y 
N+10 siendo  N la fecha de envío del 
pedido  de autorización. 
- Deberá ser mayor o igual  al  del  
ultimo  comprobante emitido  para 
ese tipo y punto  de venta 
- Para comprobantes MiPyMEs (FCE) 
estar comprendido  en el  rango  N-5 
y N+1 siendo N la fecha de envío del 
pedido  de autorización. De tratarse 
de notas de débito o crédito, la 
fecha del comprobante puede ser 
hasta N-5.
<AlicIVA> 10018
Si <ImpIVA> es igual a 0 los objetos 
<IVA> y <AlicIva> solo deben 
informarse con ImpIVA = 3 (iva 0)
Si <ImpIVA> es mayor a 0 el objeto 
<IVA> y <AlicIva> son obligatorios.
El objeto <AlicIva> es obligatorio y 
no debe ser nulo si ingresa <IVA>
No aplica para comprobantes tipo C.
<AlicIVA><id> 10019
El campo Id en AlicIVA es obligatorio 
informarlo. 
Si el tipo de comprobante es 2, 3, 7, 
8, 52 o 53  informarlo es opcional. 
Siempre que se informe Id, debe ser 
un valor devuelto por el método 
FEParamGetTiposIva.
No aplica para comprobantes tipo C.
<AlicIVA><BaseImp> 10020
El  campo  BaseImp  en AlicIVA es 
obligatorio  y debe ser mayor a 0 
cero. Excepto para comprobantes 2, 
3, 7, 8, 52 o 53 que puede ser cero o 
no ser informado.
No aplica para comprobantes tipo C.
<AlicIVA><Importe> 10021
El  campo  Importe  en AlicIVA es 
obligatorio, mayor o igual  0 cero. 
Excepto para comprobantes 2, 3, 7, 



# Página 43

Campo / Grupo Código 
de error Descripción de la validación
8, 52 o 53 que puede ser cero o no 
ser informado. 
No aplica para comprobantes tipo C.
<AlicIVA><id> 10022
El  campo  Id en AlicIVA no debe 
repetirse. Deberá totalizarse por 
alícuota. 
No aplica para comprobantes tipo C.
<ImpIVA> / 
<AlicIVA><importe>
10023
La suma de los campos <importe> en 
<IVA>   debe  ser igual al valor 
ingresado  en  ImpIVA.
Margen de error:
Error relativo porcentual deberá ser 
<= 0.01% o el error absoluto <=0.01 
* cantidad de alícuotas de IVA 
ingresadas *
No aplica para comprobantes tipo C.
<Tributo> 10024
Si ImpTrib es mayor a 0 el objeto 
<Tributos> y <Tributo> son 
obligatorios.
El objeto  <Tributo> es obligatorio y 
no deber ser nulo si se incluye el  
objeto <Tributos>
Si impTrib es igual a cero el objeto 
<Tributos> y <Tributo> no deben 
enviarse.
<Tributo><id> 10025
El  campo  <Id> en <Tributo> es 
obligatorio  y debe ser alguno de los 
devueltos por  el  método  
FEParamGetTiposTributos
<Tributo><BaseImp> 10026
El  campo  <BaseImp>  en <Tributo> 
es obligatorio  y debe ser mayor o 
igual a 0 cero 
<Tributo><Alic> 10027 El  campo  <Alic>   en <Tributo> es 
obligatorio , mayor o igual  0 cero 
<Tributo><importe> 10028 El  campo  <Importe>  en <Tributo> 
es obligatorio , mayor o igual  0 cero 
<ImpTrib> / 
<Tributo><importe>
10029
La suma de los importes en  
<Tributo>   debe  ser igual al valor 



# Página 44

Campo / Grupo Código 
de error Descripción de la validación
ingresado  en  <ImpTrib>
Margen de error:
Error relativo porcentual deberá ser  
<= 0.01% o el error absoluto <=0.01  
* cantidad de tributos *
<concepto> 10030
El campo  <Concepto>  es obligatorio 
y debe  corresponder con algún 
valor devuelto  por  el  método  
FEParamGetTiposConcepto
1 Productos
2 Servicios
3 Productos y Servicios
<FchServDesde> / <FchServHasta> / <FchVtoPago> 10031
El campo “fecha desde del servicio a 
facturar”  <FchServDesde> es 
obligatorio si se informa “fecha 
hasta del servicio a facturar”   
<FchServHasta> y/o “fecha de 
vencimiento para el pago”  
<FchVtoPago>.
<FchServDesde> / <FchServHasta> 10032
El campo “fecha desde del servicio a 
facturar”   <FchServDesde> no 
puede ser posterior al campo “fecha 
hasta del servicio a facturar”  
<FchServHasta>.
<FchServDesde> / <FchServHasta> / <FchVtoPago> 10033
El campo “fecha hasta del servicio a 
facturar”   <FchServHasta> es 
obligatorio si se informa “fecha 
desde del servicio a facturar”  
<FchServDesde> y/o “fecha de 
vencimiento para el pago”  
<FchVtoPago>.
<FchServDesde> / <FchServHasta> / <FchVtoPago> 10035
El campo “fecha de vencimiento 
para el pago”   <FchVtoPago> es 
obligatorio si se informa “fecha 
desde del servicio a facturar” 
<FchServDesde> y/o “fecha hasta 
del servicio a facturar” 
<FchServHasta>. 
<FchVtoPago> 10036 El campo “fecha de vencimiento 
para el pago”  <FchVtoPago>  no 



# Página 45

Campo / Grupo Código 
de error Descripción de la validación
puede ser anterior a la fecha del 
comprobante.
<MonId> 10037
El campo  <MonId> es obligatorio  y 
debe corresponder a algún valor 
devuelto  por  el  método 
FEParamGetTiposMonedas
<MonCotiz> 10038
El  campo <MonCotiz> es obligatorio  
si  no  informa  el  campo  
CanMisMonExt = S , y de informarse  
debe ser mayor a 0. Si se indica que  
el pago del comprobante se realiza  
en la misma moneda extranjera que  
la  factura,  la  cotización  de  la  
moneda  provista  debe  coincidir  
exactamente con la registrada en las  
bases  de  ARCA  para  el  día  hábil  
anterior  a  la  fecha  de  emisión  del 
comprobante, si esta es anterior a la  
fecha actual, o bien con la registrada  
para el día hábil anterior a la fecha  
actual,  si  la  fecha  de  emisión  es  
posterior a esta. En caso contrario,  
se  puede  omitir  el  campo  de  
Cotización de Moneda.
<MonId> / <MonCotiz> 10039
El  campo <MonCotiz> es obligatorio 
, e igual a 1  cuando  se trate de 
<MonId>=PES
<CbtesAsoc> /
<CbteTipo>
10040
De enviarse el tag <CbtesAsoc>,  
entonces el campo “código de tipo 
de comprobante”  <CbteTipo> a 
autorizar tiene que ser 01, 02, 03, 
06, 07, 08, 12, 13, 51, 52, 53, 201, 
206 o 211
Para 02 y 03 pueden asociarse los 
tipos de comprobante  01, 02, 03, 
04, 05, 34, 39, 60, 63, 88 y 991 
Para 07 y 08 pueden asociarse 06, 
07, 08, 09, 10, 35, 40, 61, 64, 88 y 
991
Para 12 o 13 pueden asociarse 11, 
12, 13 y 15.
Para 52 o 53 pueden asociarse  51, 



# Página 46

Campo / Grupo Código 
de error Descripción de la validación
52, 53, 54, 88 y 991
Para 01,06 y 51 pueden asociarse 88 
y 991
Para comprobantes MiPyMEs (FCE) 
201, 206 o 211 puede asociarse los 
comprobantes (91, 990, 991, 993, 
994, 995). 
Para comprobantes MiPyMEs (FCE) 
A 202 y 203, puede asociar 201,202 
o 203.
Para comprobantes MiPyMEs (FCE) B 
207, 208 puede asociar 206, 207, 
208.
Para comprobantes MiPyMEs (FCE) C 
212, 213 puede asociarse 211, 212, 
213.
<Tirbuto><Id> / <Tirbuto><Desc> 10042
El campo  <Desc> en Tributo es 
obligatorio cuando  se informe <Id>  
= 99.
Para comprobantes MiPyMEs (FCE) 
siempre es obligatoria la descripción
<ImpTotConc> 10043
El campo “Importe neto no gravado” 
<ImpTotConc>. No puede ser menor 
a cero (0).
Para comprobantes tipo C debe ser 
igual a cero (0).
Para comprobantes tipo 49 – Bienes 
usados, si el emisor es 
MONOTRIBUTISTA, este campo 
corresponde al importe del subtotal 
de la operación
<ImpOpEx> 10044
El campo “importe exento”  
<ImpOpEx>. No puede ser menor a 
cero (0). 
Para comprobantes tipo C debe ser 
igual a cero (0).
Para comprobantes tipo 49 – Bienes 
usados, si se encuentra inscripto en 
MONOTRIBUTO no debe informarse 



# Página 47

Campo / Grupo Código 
de error Descripción de la validación
o debe ser igual a cero (0).
<ImpNeto> 10045
El campo “Importe neto  gravado” 
<ImpNeto>. No puede ser menor a 
cero (0).
Para comprobantes tipo C este 
campo corresponde al Importe del 
Sub Total.
Para comprobantes tipo 49 – Bienes 
usados, si se encuentra inscripto en 
MONOTRIBUTO no debe informarse 
o debe ser igual a cero (0).
<ImpTrib> 10046
El campo “Importe de tributos”  
<ImpTrib>. No puede ser menor a 
cero (0).
<ImpIVA> 10047
El campo “Importe de IVA”  
<ImpIVA>. No puede ser menor a 
cero (0). 
Para comprobantes tipo C debe ser 
igual a cero (0).
Para comprobantes tipo 49 – Bienes 
usados, si se encuentra inscripto en 
MONOTRIBUTO no debe informarse 
o debe ser igual a cero (0).
<ImpTotConc> / <ImpOpEx> / <ImpNeto> / <ImpTrib> / 
<ImpIVA> / <ImpTotal> 10048
El campo  “Importe Total” 
<ImpTotal>, debe ser igual  a la  
suma de ImpTotConc + ImpNeto + 
ImpOpEx + ImpTrib + ImpIVA
Para comprobantes tipo C, el campo 
“Importe Total” <ImpTotal>, debe 
ser igual  a la  suma de ImpNeto + 
ImpTrib.
Para comprobantes tipo 49 – Bienes 
Usados, si se encuentra inscripto en 
MONOTRIBUTO el campo  “Importe 
Total” <ImpTotal>, debe ser igual  a 
la  suma de ImpTotConc + ImpTrib.
Margen de error:
Error relativo porcentual deberá ser 



# Página 48

Campo / Grupo Código 
de error Descripción de la validación
<= 0.01% o el error absoluto <=0.01
<FchServDesde> / <FchServHasta> / <FchVtoPago> 10049
Los campos <FchServDesde>, 
<FchServHasta>, <FchVtoPago>, es 
obligatorio cuando el campo 
<Concepto> es igual a 2 o 3.
Si se informa deberá tener el 
siguiente formato yyyymmdd.
<AlicIVA> 10051
Los importes informados en AlicIVA 
se deben corresponder según el tipo 
de iva seleccionado. Para 
comprobantes tipo 2, 3, 7, 8, 52 y 53 
no se tiene en cuenta esta 
validación.
Margen de error:
Error relativo porcentual deberá ser 
<= 0.01% o el error absoluto <=0.01
No aplica para comprobantes tipo C
<Opcionales> 10052 Si envía <Opcionales>, <Opcional> es 
obligatorio.
<Opcional> 10053
El campo  <Id> en <Opcionales> es 
obligatorio  y debe ser alguno de los 
devueltos por  el  método 
FEParamGetTiposOpcional.
<Opcional> 10054
El campo  <Id> en <Opcionales> es 
obligatorio  y no debe repetirse. 
Solo pueden repetirse los 
identificadores 1801 y 1802  
informados para la RG 4004-E.
<Opcional> 10055 El campo  <Valor> en Opcionales es 
obligatorio
Importes en general 10056 Que se informen los mismos con la 
precisión indicada.
<CbteAsoc><Tipo> 
10057
De enviarse el tag CbteAsoc debe 
enviarse Tipo > a 0
<CbteAsoc><PtoVta> 10058 De enviarse el tag CbteAsoc debe 
enviarse PtoVta > a 0 y < a 99999
<CbteAsoc><Nro> 10059 De enviarse el tag CbteAsoc debe 
enviarse Nro > a 0 y < a 99999999



# Página 49

Campo / Grupo Código 
de error Descripción de la validación
<CbteAsoc><Tipo> / <CbteAsoc><PtoVta> / 
<CbteAsoc><Nro> 10060
De enviarse el tag CbteAsoc, los 
comprobantes no deben repetirse.
<ImpNeto> / 
<AlicIVA><BaseImp>
10061
La suma de los campos <BaseImp> 
en  <AlicIva>   debe  ser igual al valor 
ingresado  en  ImpNeto. Esta 
validación no deberá ser tenida en 
cuenta, cuando el <CbteTipo> sea 
02, 03 ,07, 08, para comprobantes 
tipo C (11, 12, 13, 15) y para 
Comprobantes tipo M (52, 53) 
Margen de error:
Error relativo porcentual deberá ser  
<= 0.01% o el error absoluto <=0.01  
*  cantidad  de  alícuotas  de  IVA  
ingresadas *
<CbtesAsoc><CbteAsoc> 10062 Si envía <CbtesAsoc>, <CbteAsoc> es 
obligatorio.
<Opcionales><Id><Valor>
10064 Si selecciona Id = 2 el valor ingresado 
debe ser un numérico de 8 (ocho) 
dígitos mayor o igual a 0 (cero). 
<ImpTotal> 10065
El campo “Importe Total”  
<ImpTotal>. No puede ser menor a 
cero (0). 
<Opcionales><Id><Valor> 10066
Si Id = 2 y el comprobante 
corresponde a una actividad 
alcanzada por el beneficio de 
Promoción Industrial en el campo 
<Valor> se deberá informar el 
número identificador del proyecto 
(el mismo deberá corresponder a la 
CUIT emisora del comprobante), si 
no corresponde a una actividad 
alcanzada por el beneficio el campo 
<Valor> deberá ser 0 (cero).
El Id = 2 solo podrá informarse 
cuando <CbteTipo> es igual a 1, 2, 3, 
6, 7, 8.
<ImpTrib> <DocTipo><DocNro> 10067
Para comprobantes tipo B, si 
<DocTipo> es 80 y <DocNro> es 
23000000000 (No Categorizado), 



# Página 50

Campo / Grupo Código 
de error Descripción de la validación
ImpTrib debe ser mayor a 0.
<Opcionales><CbteTipo> 10068
El array <Opcionales> no es 
obligatorio. Solo puede informarse si 
<CbteTipo> es 1, 2, 3, 4, 6, 7, 8, 9, 
11,12, 13, 15, 49, 51, 52, 53, 54, 201, 
206, 211, 203, 208, 213, 202, 207, 
212.
<DocNro> 10069
El N° de documento del receptor del 
comprobante no puede ser igual al 
del emisor.
<ImpNeto>/
<Iva>
10070
Si el importe neto es mayor a cero, 
es obligatorio informar el array de 
iva.
<Iva> 10071 Si el tipo de comprobante es C, el 
array de IVA no debe informarse.
<CbteTipo>/<AlicIVA> 10075
Si el comprobante informado es tipo 
49 – Bienes Usados, el emisor del 
comprobante se encuentra inscripto 
en el MONOTRIBUTO. El objeto 
<IVA> y <AlicIva> no deben 
informarse.
<Opcionales><CbteTipo>/<DocTipo> 10076
Si el comprobante informado es tipo 
49 – Bienes Usados, es obligatorio 
informar opcionales. 
Ver  método  
FEParamGetTiposOpcional()
<Opcionales><Id>/
<Opcionales><Valor>
10077
Si informa Id = 91 el valor ingresado 
no puede ser un blanco y debe ser 
un alfanumérico de 100 caracteres 
como máximo.
<Opcionales><Id>/<CbteTipo> 10078
Si el comprobante es del tipo 49 – 
Bienes Usados es obligatorio 
informar el Nombre y Apellido 
mediante el ID = 91.
<Opcionales><Id>/
<Opcionales><Valor>
 10079
Si informa Id = 92 el valor ingresado 
debe ser un valor numérico de 3 
posiciones. 
<Opcionales><Id>/ 10080 Si informa Id = 92, el contenido del 



# Página 51

Campo / Grupo Código 
de error Descripción de la validación
<Opcionales><Valor>
campo <Valor> debe corresponder a 
un código de país valido. Ver 
método FEParamGetTiposPaises
<Opcionales><Id>/<CbteTipo> 10081
Si el comprobante es del tipo 49 – 
Bienes Usados, los valores posibles 
para el id son  91, 92, 93.
<Opcionales><Id>/
<CbteTipo> 10082
Si en el campo TipoDoc se informa 
30, 91 o 94 se deberá informar el id 
92 con el código del país del 
vendedor. Consultar Método 
FEParamGetTiposPaises.
Si TIPODOC es distinto de 30, 91 o 94 
no debe informarse el id 92.
<Opcionales><Id>/
<Opcionales><Valor>
10083
Si informa Id = 93, el valor ingresado 
no puede ser blanco y debe ser 
alfanumérico de 250 caracteres 
como máximo
<Opcionales><Id>/<CbteTipo> 10084
Si el comprobante es del tipo 49 – 
Bienes Usados es obligatorio 
informar el Domicilio del 
receptor/vendedor el ID = 93.
<concepto> 10085 Para comprobantes tipo 49 – Bienes 
usados, solo informar 1 – Productos
<Opcionales><Id>/<CbteTipo> 10086
Si el comprobante es del tipo A (1, 2, 
3, 4, 5, 34, 39, 60, 63) e intenta 
informar datos opcionales según 
Resolución General 3668, los valores 
posibles para los identificadores son 
5, 61, 62, 7.
<Opcionales><Id>/
<Opcionales><Valor>
10088
Si informa Id = 5, el valor ingresado 
no puede ser blanco y debe ser 
alfanumérico de 2 caracteres.
<Opcionales><Id>/
<Opcionales><Valor>
10089
Si informa Id = 5, el contenido del 
campo <Valor> debe corresponder a 
un código de EXCEPCION válido 
comprendido por alguno de los sig:
01 – Locador / Prestador del mismo
02 – Congresos / Eventos
03 – Operación contemplada en RG 
74



# Página 52

Campo / Grupo Código 
de error Descripción de la validación
04 – Bienes de Cambio
05 – Ropa de trabajo
06 – Intermediario 
<Opcionales><Id>/
<Opcionales><Valor>
10090
Si informa Id = 61, el valor ingresado 
no puede ser blanco y debe ser 
numérico de 2 caracteres.
<Opcionales><Id>/
<Opcionales><Valor>
10091
Si informa Id = 61, el contenido del 
campo <Valor> debe corresponder a 
un código que represente el tipo de 
documento del firmante. Ver 
método FEParamGetTiposDoc.
<Opcionales><Id>/
<Opcionales><Valor>
10092
Si informa Id = 62, el valor ingresado 
no puede ser blanco y debe ser 
numérico de 11 caracteres como 
máximo.
<Opcionales><Id>/
<Opcionales><Valor>
10093
Se encuentra dado de baja mediante 
redmine RM57235
<Opcionales><Id>/
<Opcionales><Valor>
10094
Si informa Id = 7, el valor ingresado 
no puede ser blanco y debe ser 
numérico de 2 caracteres.
<Opcionales><Id>/
<Opcionales><Valor>
10095
Si informa Id = 7, el contenido del 
campo <Valor> debe corresponder a 
un código de carácter firmante 
válido comprendido por alguno de 
los sig:
01 – Titular
02 – Director / Presidente
03 – Apoderado
04 – Empleado
<PtoVta> / <CbteTipo> 10096
Para comprobantes tipo C, si el 
contribuyente se encuentra 
registrado en las bases del 
organismo como exento, el punto de 
venta a utilizar al momento de 
autorizar el comprobante debe ser 
del tipo “COMPROBANTES – EXENTO 
EN IVA – WEB SERVICES”.
<Opcionales><Id>/ 10097 Si informa id = 10 (RG 3.368 
Establecimientos de educación 



# Página 53

Campo / Grupo Código 
de error Descripción de la validación
<Opcionales><Valor>
pública de gestión privada), el valor 
ingresado no puede ser blanco y 
debe ser un numerico de 1 carácter:
0 – Actividades no comprendidas 
1 – Actividades comprendidas
<Opcionales><Id>/
<Opcionales><Valor>
10098
Si informa id = 1011  (RG 3.368 
Establecimientos de educación 
pública de gestión privada), el valor 
ingresado no puede ser blanco y 
debe corresponder al tipo de 
documento del titular del pago. Ver 
método FEParamGetTiposDoc.
<Opcionales><Id>/
<Opcionales><Valor>
10099
Si informa id = 1012  (RG 3.368 
Establecimientos de educación 
pública de gestión privada), el valor 
ingresado no puede ser blanco y 
debe corresponder al n° de 
documento del titular del pago. 
Numérico de 11 caracteres como 
máximo para tipo de documento 80, 
86, 87, 96 o alfanumérico de 20 
como máximo para el resto de los 
tipos de documentos.
<Opcionales><Id>/
<Opcionales><Valor>
10110
Si informa id = 11 (RG 2.820 
Operaciones económicas vinculadas 
con bienes inmuebles), el valor 
ingresado no puede ser blanco y 
debe ser un numérico de 1 carácter:
0 – Actividades no comprendidas 
1 – Actividades comprendidas
<Opcionales><Id>/
<Opcionales><Valor>
10111
Si informa id = 12 (RG 3.687 
Locación temporaria de inmuebles 
con fines turísticos), el valor 
ingresado no puede ser blanco y 
debe ser un numérico de 1 carácter:
0 – Actividades no comprendidas 
1 – Actividades comprendidas
<Opcionales><Id>
10112
Si intenta informar datos opcionales 
según Resolución General:
RG 3.368 Establecimientos de 



# Página 54

Campo / Grupo Código 
de error Descripción de la validación
educación pública de gestión privada 
(identificador 10)
RG 2.820 Operaciones económicas 
vinculadas con bienes inmuebles 
(identificador 11)
RG 3.687 Locación temporaria de 
inmuebles con fines turísticos 
(identificador 12).
RG 2.863 Representantes de 
Modelos (identificador 13).
RG 2.863 Agencias de publicidad 
(identificador 14).
RG 2.863 Personas físicas que 
desarrollen actividad de modelaje 
(identificador 15).
RG 4004-E Alquiler de inmuebles con 
destino casa habitación 
(identificador 17, en caso de ser 
necesario informar titular o 
cotitular, el identificador que 
acompaña al 17 es el 1801 y 1802).
Recordar que en un mismo 
comprobante solo puede informar 
identificadores opcionales para solo 
1 resolución por comprobante.
<Opcionales><Id>/
<Opcionales><Valor>
10113
Si informa id = 10 (RG 3.368 
Establecimientos de educación 
pública de gestión privada) con valor 
“1 – Actividades comprendidas”
Informar 
1011 – Tipo de Documento
1012 – N° de documento
Si informa id = 10 (RG 3.368 
Establecimientos de educación 
pública de gestión privada) con valor 
“0 – Actividades No comprendidas”
No informar 



# Página 55

Campo / Grupo Código 
de error Descripción de la validación
1011 – Tipo de Documento
1012 – N° de documento
<Opcionales><Id>/
<Opcionales><Valor>
10114
Si informa id = 1011 o 1012 (RG 
3.368 Establecimientos de educación 
pública de gestión privada)  es 
obligatorio informar el identificador 
que representa si se encuentra 
comprendida (id = 10, valor = 1)
<Opcionales><Id>/
<Opcionales><Valor>
10115
Si informa id = 10 (RG 3.368 
Establecimientos de educación 
pública de gestión privada) con valor 
“1 – Actividades comprendidas” e 
informa
ID = 1011 (Tipo de Documento) con 
un valor que se corresponde al 80, 
86, 87, 96 (CUIT, CUIL, CDI, DNI 
respectivamente),  deberá 
verificarse que el número 
consignado en el ID = 1012 (n° de 
documento del titular del pago), se 
encuentra en los padrones de arca.
<Opcionales><Id>/
<Opcionales><Valor>
10116
Si informa id = 13 (RG 2.863 
Representantes de Modelos), el 
valor ingresado no puede ser blanco 
y debe ser un numérico de 1 
carácter:
0 - Actividades no comprendidas 
1 - Actividades comprendidas
<Opcionales><Id>/
<Opcionales><Valor>
10117
Si informa id = 14 (RG 2.863 
Agencias de publicidad), el valor 
ingresado no puede ser blanco y 
debe ser un numérico de 1 carácter:
0 - Actividades no comprendidas 
1 - Actividades comprendidas
<Opcionales><Id>/
<Opcionales><Valor>
10118
Si informa id = 15 (RG 2.863 
Personas físicas que desarrollen 
actividad de modelaje), el valor 
ingresado no puede ser blanco y 
debe ser un numérico de 1 carácter:
0 - Actividades no comprendidas 



# Página 56

Campo / Grupo Código 
de error Descripción de la validación
1 - Actividades comprendidas
<MonId>/<MonCotiz> 10119
Si la moneda es <> PES, el tipo de 
cambio no podrá ser inferior al 2% ni 
superior en un 400% del que 
suministra arca como orientativo de 
acuerdo a la cotización oficial. 
Para poder obtener la cotización ver 
Metodo FEParamGetCotizacion.
<CbteAsoc><Tipo> / <CbteAsoc><PtoVta> / 
<CbteAsoc><Nro> 10120
Si informa comprobantes asociados, 
y sus códigos son 91, 88, 988, 990, 
991, 993, 994, 995, 996, 997, los 
mismos deben encontrarse 
registrados.
<CbteAsoc><Tipo> / <CbteAsoc><PtoVta> / 
<CbteAsoc><Nro> 10121
Si informa comprobantes asociados, 
y sus códigos son 91, 88, 988, 990, 
991, 993, 994, 995, 996, 997, los 
mismos deben encontrarse 
confirmados.
<DocTipo> / <DocNro>
<CbteAsoc><Cuit>
10122
Si informa comprobantes asociados 
y sus códigos son 91, 88, 988, 990, 
991, 993, 994, 995, 996, 997, el 
receptor del comprobante a 
autorizar debe ser igual al receptor 
del comprobante asociado.
<Opcionales><Id>/
<Opcionales><Valor>
10123
Si el comprobante es del tipo B o C e 
intenta informar datos opcionales 
según Resolución General 4004-E, 
los valores posibles para los 
identificadores son 17, 1801, 1802.
<Opcionales><Id>/
<Opcionales><Valor>
10124
Si informa id = 17 (RG 4004-E 
Locación de inmuebles destino 
"casa-habitación"), el valor 
ingresado no puede ser blanco y 
debe ser un numérico de 1 carácter:
1 (uno) = facturación a través de 
intermediario
2 (dos) = facturación directa 
<Opcionales><Id>/
<Opcionales><Valor>
10125
Si informa id = 1801  (RG 4004-E 
Locación de inmuebles destino 
"casa-habitación"), el valor 
ingresado no puede ser blanco y 
debe corresponder al CUIT del 



# Página 57

Campo / Grupo Código 
de error Descripción de la validación
propietario/locador. Numérico de 11 
caracteres.
<Opcionales><Id>/
<Opcionales><Valor>
10126
Si informa Id = 1801 (RG 4004-E 
Locación de inmuebles destino 
"casa-habitación"), verificar que el 
número consignado se encuentra en 
los padrones de arca.
<Opcionales><Id>/
<Opcionales><Valor>
10127
Si informa Id = 1802 (RG 4004-E 
Locación de inmuebles destino 
"casa-habitación") el valor ingresado 
no puede ser un blanco y debe ser 
un alfanumérico de 100 caracteres 
como máximo que representa el 
Nombre y Apellido 
propietario/locador.
<Opcionales><Id>/
<Opcionales><Valor>
10128
Si informa Id = 1801 (RG 4004-E 
Locación de inmuebles destino 
"casa-habitación") con un CUIT 
propietario/locador no repetirlo 
dentro de la lista de 
propietarios/locadores. 
<Opcionales><Id>/
<Opcionales><Valor>
10129
Si informa id 17 con valor 1 
(Intermediario) (RG 4004-E Locación 
de inmuebles destino "casa-
habitación"), deben informarse 
obligatoriamente los identificadores 
1801 y 1802.
Si informa id 17 con valor 2 (Directo) 
(RG 4004-E Locación de inmuebles 
destino "casa-habitación"), pueden 
no informarse los identificadores 
1801, 1802. Solo informarlos cuando 
hay otro/s propietarios/locadores.
<Opcionales><Id>/
<Opcionales><Valor>
10130
Si informa id 1801 y id 1802 (RG 
4004-E Locación de inmuebles 
destino "casa-habitación"), la 
cantidad de opcionales con id 1801 y 
1802 deben ser iguales.
<Opcionales><Id>/
<Opcionales><Valor>
<Auth><Cuit>
10131
Si informa Id = 1801 (RG 4004-E 
Locación de inmuebles destino 
"casa-habitación") con un CUIT 
propietario/locador no puede ser el 
mismo que el emisor del 



# Página 58

Campo / Grupo Código 
de error Descripción de la validación
comprobante.
<Opcionales><Id>/
<Opcionales><Valor>
10132
Si informa id 1801 y id 1802 (RG 
4004-E Locación de inmuebles 
destino "casa-habitación"), es 
obligatorio informar el id 17 con 
valor 1 (Intermediario) o 2 (Directo).
<Compradores>/<Comprador> 10133 Si envía compradores, comprador es 
obligatorio y no debe ser vacío.
<FeCabReq><CbteTipo>/
<Compradores>
10134
La estructura compradores se 
encuentra habilitada para 
comprobantes tipo A, B, C o M.
<Compradores>/<Comprador>/<DocTipo> 10135
Si envía compradores, el tipo de 
documento del comprador es 
obligatorio informarlo.
<FECAEDetRequest><DocTipo>/
<Compradores>
10136
Solo informar compradores cuando 
el tipo de documento del receptor 
del comprobante es 80, 86, 87  
(CUIT, CUIL, CDI respectivamente).
<Compradores>/<Comprador>/<DocTipo> 10137
Los tipos de documentos habilitados 
a informar sobre el comprador son 
80, 86, 87  (CUIT, CUIL, CDI 
respectivamente). 
<Compradores>/<Comprador>/<DocNro> 10138
Si envía compradores, el número de 
documento del comprador es 
obligatorio informarlo.
<Compradores>/<Comprador>/<DocNro> 10139
Si envía compradores, el número de 
documento debe ser un documento 
con formato válido, numérico de 11 
caracteres.
<Auth>/<Cuit>
<Compradores>/<Comprador>/<DocNro>
10140
Si envía compradores, el número de 
documento del comprador no puede 
ser igual al número de documento 
del emisor del comprobante
<Compradores>/<Comprador> 10141 Si envía compradores, los mismos no 
pueden repetirse en la lista.
<Compradores>/<Comprador>/<Porcentaje> 10142
Si envía compradores, el porcentaje 
de titularidad del comprador es 
obligatorio informarlo.
<Compradores>/<Comprador>/<Porcentaje> 10143
Si envía compradores, el porcentaje 
de titularidad debe ser un valor 
numérico de 2 enteros y 2 



# Página 59

Campo / Grupo Código 
de error Descripción de la validación
decimales, los cuales deben ser 
valores mayores a cero.
<Compradores>/<Comprador>/<Porcentaje> 10144 Si envía compradores, el porcentaje 
de titularidad debe ser mayor a cero.
<Compradores>/<Comprador> 10145
Si envía compradores, los 
compradores informados deben ser 
al menos 2. Uno de los dos debe ser 
el receptor del comprobante.
<FECAEDetRequest><DocTipo>
<FECAEDetRequest><DocNro>
<Compradores>/<Comprador>/<DocTipo>
<Compradores>/<Comprador>/<DocNro>
10146
Si envía compradores, el comprador 
de mayor porcentaje de titularidad 
debe coincidir con el receptor del 
comprobante.
<Compradores>/<Comprador>/<Porcentaje> 10147
Si envía compradores, la sumatoria 
de todos los porcentajes de 
titularidad debe ser del 100%.
<Compradores>/<Comprador>/<DocNro> 10148
Si envía compradores, los 
compradores deben encontrarse 
registrados en el padrón de arca, en 
condición activa.
<Compradores>/<Comprador>/<DocNro> 10149
Si envía compradores, y el tipo de 
comprobante es A o M, el receptor o 
al menos uno de los compradores 
deben encontrarse registrados de 
forma activa en el Impuesto al Valor 
Agregado o Responsable 
Monotributo.
<FECAEDetRequest><Concepto>/
<Compradores>
10150
Solo enviar compradores cuando el 
concepto es 1 – PRODUCTO
<CbteAsoc><Cuit> 10151
Si informa Cuit en comprobantes 
asociados, no informar en blanco, el 
mismo debe ser un valor de 11 
caracteres numéricos. 
Para comprobante del tipo MiPyMEs 
(FCE) del tipo débito o crédito es 
obligatorio informar el campo.
<CbteFch>/<Concepto> 10152
Si informa fecha de comprobante 
<CbteFch> para el Concepto del tipo 
“01 – Productos” con fecha superior 
a la fecha de envío de autorización, 
el mes de la fecha del comprobante 



# Página 60

Campo / Grupo Código 
de error Descripción de la validación
<CbteFch> debe coincidir con el mes 
de la fecha de envío de autorización.
Si informa fecha de comprobante 
<CbteFch> para comprobantes del 
tipo MiPyMEs (FCE) con fecha 
superior a la fecha de envío de 
autorización, el mes de la fecha del 
comprobante <CbteFch> debe 
coincidir con el mes  de la fecha de 
envío de autorización.
<FeCabReq><CbteTipo>/
<FECAEDetRequest><CbtesAsoc>
10153
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE) y 
corresponde a un comprobante de 
débito o crédito, es obligatorio 
informar comprobantes asociados.
<FeCabReq><CbteTipo>/
<CbteAsoc><Tipo><PtoVta><Nro><Cuit>
10154
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE) y 
corresponde a un comprobante de 
débito o crédito. Tener en cuenta 
que:
- sí el comprobante asociado  se 
encuentra rechazado por el 
comprador hay que informar el 
código de anulación 
correspondiente sobre el campo 
"Adicionales por RG",  códigos 22  -  
Anulación. Valor “S”
- sí el comprobante asociado  no se 
encuentra rechazado por el 
comprador hay que informar el 
código de no anulación 
correspondiente sobre el campo 
"Adicionales por RG",  códigos 22  -  
Anulación. Valor “N”
<Auth><Cuit>
<FeCabReq><CbteTipo>/
<CbteAsoc><Cuit>
10155
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), el 
CUIT del emisor del comprobante 
asociado debe coincidir con el CUIT 
del emisor del comprobante a 
autorizar. 
<FeCabReq><CbteTipo>/ 10156 Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), 



# Página 61

Campo / Grupo Código 
de error Descripción de la validación
<CbteAsoc><Tipo>
Débito o Crédito sin código de 
Anulación siempre debe asociar 1 
comprobante tipo factura
<FeCabReq><CbteTipo>/
<CbteAsoc><Tipo>
10157
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), 
Débito o Crédito sin código de 
Anulación solo puede asociar:
Para comprobantes A, asociar 201 o 
(91, 88, 988, 990, 991, 993, 994, 995, 
996, 997). 
Para comprobantes B, asociar 206 o  
(91, 88, 988, 990, 991, 993, 994, 995, 
996, 997).
Para comprobantes C, asociar 211 o  
(91, 88, 988, 990, 991, 993, 994, 995, 
996, 997).
<FeCabReq><CbteTipo>/
<CbteAsoc> / <Tipo> / <PtoVta> /
<Nro> / <Cuit> / <CbteFch>
10158
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), 
Débito o Crédito, es obligatorio 
informar la fecha del comprobante 
asociado
<FeCabReq><CbteTipo>/
<FeDetReq>/<CbteFch>/
<CbteAsoc> / <Tipo> / <PtoVta> /
<Nro> / <Cuit> / <CbteFch>
10159
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), 
Débito o Crédito, la fecha del 
comprobante asociado tiene que ser 
igual o menor a la fecha del 
comprobante que se está 
autorizando
<FeCabReq><CbteTipo>/
<CbteAsoc> / <Tipo> / <PtoVta> /
<Nro> / <Cuit> / <CbteFch>
10160
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), 
Débito o Crédito, el comprobante 
debe existir autorizado en las bases 
de esta Administración con la misma 
fecha informada en el asociado. 
<FeCabReq><CbteTipo>/
<FECAEDetRequest><DocTipo><DocNro>
10161
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), el 
receptor del comprobante debe 
tener habilitado el domicilio fiscal 
electrónico
<FeCabReq><CbteTipo>/
<Opcionales>
10162
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE) 



# Página 62

Campo / Grupo Código 
de error Descripción de la validación
es obligatorio informar <Opcionales>
<FeCabReq><CbteTipo>/
<FchVtoPago>
10163
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), Tipo 
201 - FACTURA DE CREDITO ELEC-
TRONICA MiPyMEs (FCE) A / 206 - 
FACTURA DE CREDITO ELECTRONICA 
MiPyMEs (FCE) B / 211 - FACTURA 
DE CREDITO ELECTRONICA MiPyMEs 
(FCE) C,  es obligatorio informar Fch-
VtoPago
<FeCabReq><CbteTipo>/
<FchVtoPago> / <FECAEDetRequest><CbteFch>
10164
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), la fe-
cha de vencimiento de pago  <FchV-
toPago> debe ser posterior o igual a 
la fecha de emisión <CbteFch> o fe-
cha de presentación (fecha actual), 
la que sea posterior
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10165
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), in-
forma opcionales, el valor correcto 
para el código 2101 es un CBU nu-
mérico de 22 caracteres.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10166
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), in-
forma opcionales, el valor correcto 
para el código 2102 es un ALIAS alfa-
numérico de 6 a 20 caracteres.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10167
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), in-
forma opcionales, el valor correcto 
para el código 22 es “S” o “N”:
S = Es de Anulación
N = No es de Anulación
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10168
Si el tipo de comprobante que está 
autorizando es Factura (201, 206, 
211) del tipo MiPyMEs (FCE), infor-
ma opcionales, es obligatorio infor-
mar CBU.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10169
Si el tipo de comprobante que está 
autorizando NO es MiPyMEs (FCE), 
no informar los códigos 2101, 2102, 
22, 27
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10170
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), es 
obligatorio informar al menos uno 
de los sig. códigos 2101, 22, 27.



# Página 63

Campo / Grupo Código 
de error Descripción de la validación
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10171
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), Fac-
tura (201, 206, 211), no informar Có-
digo de Anulación 
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10172
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), Debi-
to (202, 207, 212) o Crédito (203, 
208, 213) No informar CBU,  ALIAS y 
Transferencia.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10173
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), Debi-
to (202, 207, 212) o Crédito (203, 
208, 213) informar Código de Anula-
ción
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10174
Si el tipo de comprobante que está 
autorizando es factura MiPyMEs 
(FCE), el CBU debe estar registrado 
en las bases de esta administración, 
vigente y pertenecer al emisor del 
comprobante.
<FeCabReq><CbteTipo>/
<FchVtoPago> 
10175
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), el 
campo “fecha de vencimiento para 
el pago”  <FchVtoPago>  no debe 
informarse si NO es Factura de 
Crédito. En el caso de ser Débito o 
Crédito, solo puede informarse si es 
de Anulación.
<FeCabReq><CbteTipo>/
<CbteTipo> / <DocNro>
10176
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), el 
campo DocNro para comprobantes 
deberá ser un valor registrado en el 
padrón de arca, en condición activa.
<FeCabReq><CbteTipo>/
<CbteTipo> / <DocNro>
10177
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE) del 
tipo A, el receptor del comprobante 
informado en DocTipo y  DocNro 
debe corresponder a un 
contribuyente activo en el Impuesto 
al Valor Agregado.
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE) del 
tipo B, el receptor del comprobante 
informado en DocTipo y  DocNro 
debe corresponder a un 



# Página 64

Campo / Grupo Código 
de error Descripción de la validación
contribuyente activo en el Impuesto 
Iva, Monotributo o Exento.
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE) del 
tipo C, el receptor del comprobante 
informado en DocTipo y  DocNro 
debe corresponder a un 
contribuyente activo en el Impuesto 
Iva, Monotributo o Exento.
<FeCabReq><CbteTipo>/
<CbteTipo> / <DocNro>
10178
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE) no se 
permite informar DocNro 
23000000000 (No Categorizado)
<FeCabReq><CbteTipo>/
<CbteTipo> / <DocNro>
10180
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), el 
receptor del comprobante 
informado en DocTipo y  DocNro 
debe corresponder a un 
contribuyente caracterizado como 
GRANDE o que opto por PYME. Su 
activida principal debe 
corresponderse con alguna de las 
alcanzadas por el régimen. 
<FeCabReq><CbteTipo>/<MonId>
<CbteAsoc> 10181
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), débi-
to o crédito, el mismo debe tener la 
misma moneda que el comprobante 
asociado o Pesos para ajuste en las 
diferencias de cambio (post acepta-
ción/rechazo)
<FeCabReq><CbteTipo>/
<FECAEDetRequest><DocTipo><DocNro>/
<CbteAsoc>
10183
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), es 
débito o crédito, deben coincidir 
emisores y receptores.
Si el comprobante ES de anulación, 
para autorizar un débito, el tipo de 
comprobante a asociar debe ser 
crédito y para autorizar un crédito, 
el tipo de comprobante a asociar 
debe ser una factura o un débito.
<FeCabReq><CbteTipo>/<MonId> 10184 Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), es 



# Página 65

Campo / Grupo Código 
de error Descripción de la validación
<CbteAsoc>/
<FeCabReq><ImpTotal>
crédito el monto del comprobante a 
autorizar no puede ser mayor o igual 
al saldo actual de la cuenta 
corriente. Ver micrositio factura de 
crédito
<FeCabReq><CbteTipo>/
<CbteAsoc>/ 10186
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), es 
crédito o débito A, de anulación, 
solo se encuentra habilitado asociar 
un comprobante de crédito A. 
Utilizar debito para anular crédito o 
utilizar crédito para anular débito o 
factura. 
<FeCabReq><CbteTipo>/
<CbteAsoc>/ 10187
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), es 
crédito o débito B, de anulación, 
solo se encuentra habilitado asociar 
un comprobante de crédito B. 
Utilizar debito para anular crédito o 
utilizar crédito para anular débito o 
factura.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10189
Puede identificar una o varias 
Referencias Comerciales según 
corresponda. Informar bajo el 
código 23. Campo alfanumérico de 
50 caracteres como máximo.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10190
Si informa opcionales con más de un 
identificador 23 – Referencia 
Comercial, no repetir el valor. 
<Auth><Cuit>/
<FeCabReq><CbteTipo>/
<FECAEDetRequest><DocNro>/
<FECAEDetRequest><ImpTotal> / 
<FECAEDetRequest><MonCotiz> / Tope
10192 Según la categorización de las CUITs 
emisora y receptora y el monto 
facturado debe realizar una factura  
de crédito electrónica MiPyMEs 
(FCE). Ver micrositio.
<FeCabReq><CbteTipo>/
<CbteAsoc>/ 10193
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), es 
crédito o débito C, de anulación, 
solo se encuentra habilitado asociar 
un comprobante de crédito C. 
Utilizar debito para anular crédito o 
utilizar crédito para anular débito o 
factura.



# Página 66

Campo / Grupo Código 
de error Descripción de la validación
<FeCabReq><CbteTipo>/
<Compradores>
10194
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), no se 
encuentra habilitado informar 
compradores.
<DocTipo>/
<DocNro>/
10195
Si el tipo de documento del receptor 
del comprobante que está 
autorizando es CUIT (código Tipo de 
Documento 80) y la CUIT se 
encuentra inactiva por haber sido 
incluida en la consulta de facturas 
apócrifas.
<FeCabReq><CbteTipo>/
<FECAEDetRequest><PeriodoAsoc>
10196
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), no se 
encuentra habilitado informar 
PeriodoAsoc.
<FeCabReq><CbteTipo>/
<FECAEDetRequest><CbtesAsoc>/
<FECAEDetRequest><PeriodoAsoc>
10197
Si el comprobante es Debito o 
Credito, se deberá informar de 
forma obligatoria los campos Fecha 
Comprobantes Asociados 
Desde/Hasta, o al menos un 
comprobante asociado.
<FeCabReq><CbteTipo>/
<FECAEDetRequest><PeriodoAsoc>
10198
Si el comprobante es Factura no se 
deberá informar los campos Fecha 
Comprobantes Asociados 
Desde/Hasta
<FECAEDetRequest><PeriodoAsoc><FchDesde> 10199 Si envía estructura <PeriodoAsoc> es 
obligatorio enviar <FchDesde>.
<FECAEDetRequest><PeriodoAsoc><FchHasta> 10203 Si envía estructura <PeriodoAsoc> es 
obligatorio enviar <FchHasta>.
<FECAEDetRequest><PeriodoAsoc><FchDesde> 10204
El campo <PeriodoAsoc> 
<FchDesde> debe corresponder a 
una fecha valida con formato 
YYYYMMDD
<FECAEDetRequest><PeriodoAsoc><FchHasta> 10205
El campo PeriodoAsoc.FchHasta 
debe corresponder a una fecha 
valida con formato YYYYMMDD
<FECAEDetRequest><PeriodoAsoc><FchDesde>/
<FECAEDetRequest><PeriodoAsoc><FchHasta>
10206
Las fechas informadas en 
<PeriodoAsoc> deben ser superiores 
a 01/01/2006
<FECAEDetRequest><PeriodoAsoc><FchDesde>/
<FECAEDetRequest><PeriodoAsoc><FchHasta>
10207
Las fechas informadas en 
<PeriodoAsoc>,<FchHasta> debe ser 
superior o igual a <FchDesde>.



# Página 67

Campo / Grupo Código 
de error Descripción de la validación
<FECAEDetRequest><PeriodoAsoc><FchHasta> 10208
Las fecha informada en 
<PeriodoAsoc>.<FchHasta> debe ser 
anterior o igual a la fecha de emisión 
del comprobante que estamos 
autorizando 
<FECAEDetRequest><CbteFch>/
<FECAEDetRequest><CbtesAsoc><CbteFch>
10210
Si el comprobante asociado se 
autorizó de forma electrónica y tiene 
una fecha de emisión posterior a la 
fecha de emisión del comprobante 
por el cual se está solicitando la 
autorización, ambos deberán ser del 
mismo mes/año.
<FECAEDetRequest><CbtesAsoc><CbteFch> 10211
Informar de forma obligatoria la 
fecha de Emisión del comprobante 
asociado si el punto de venta del 
comprobante asociado es 
Controlador Fiscal o FactuWeb y el 
tipo de Comprobante asociado es 
Factura, Recibo, Nota de 
Débito/Nota de Crédito
<FECAEDetRequest><CbtesAsoc><CbteFch> 10212
De informar fecha de Emisión del 
comprobante asociado y el punto de 
venta del comprobante asociado es 
Controlador Fiscal o FactuWeb, la 
fecha no puede ser posterior al día 
de hoy.
<FECAEDetRequest><CbtesAsoc><CbteFch> 10213 Si se informan deben tener el 
siguiente formato yyyymmdd.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10214
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), 
informa opcionales, el tipo de dato 
correcto para el código 27 es un 
alfanumérico de 3 caracteres. 
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
10215
Si el tipo de comprobante que está 
autorizando es MiPyMEs (FCE), 
informa opcionales y el código es 27, 
los valores posibles son:
SCA = "TRANSFERENCIA AL SISTEMA 
DE CIRCULACION ABIERTA"
ADC = "AGENTE DE DEPOSITO 
COLECTIVO"
<FeCabReq><CbteTipo>/ 10216 Si el tipo de comprobante que está 



# Página 68

Campo / Grupo Código 
de error Descripción de la validación
<Opcionales><Id><Valor>
autorizando es Factura del tipo 
MiPyMEs (201, 206, 211),  es 
obligatorio informar <Opcionales> 
con id = 27. Los valores posibles son 
SCA o ADC.
<FECAEDetRequest><Actividades><Actividad> 10218 Si envía estructura de Actividades, 
Actividad es obligatorio enviarlo.
<FECAEDetRequest><Actividades><Actividad><Id> 10219
Si envía estructura de Actividades, 
Actividad es obligatorio enviarlo y no 
debe estar vacío.
<FECAEDetRequest><Actividades><Actividad><Id> 10220
El identificador de actividad 
informado tiene que ser una de las 
actividades habilitadas. Consultar 
método FEParamGetActividades.
<FECAEDetRequest><Actividades><Actividad><Id> 10221 De enviarse el tag Actividades, las 
actividades no deben repetirse.
<FECAEDetRequest><Actividades><Actividad><Id> 10222
De enviarse actividades, las mismas 
no deben corresponder a distintos 
grupos según RG. Es decir, si informa 
actividades Cárnicas, no pueden 
estar combinadas con actividades 
Harineras, de Tabaco, etc.
<FECAEDetRequest><Actividades><Actividad><Id> 10223
De enviarse actividades, las mismas 
deben encontrarse activas para el 
emisor del comprobante. Ver 
método FEParamGetActividades.
<FECAEDetRequest><Actividades><Actividad><Id> / 
<Concepto> 10224
De enviarse actividades 
pertenecientes al grupo de 
actividades Cárnicas, las mismas 
deben enviarse con comprobantes 
con Concepto del tipo Producto o 
Productos y Servicios.
<FECAEDetRequest><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc><Tipo>
10225
Si las actividades informadas 
corresponden a actividades Cárnicas, 
informar remito asociado 995 - 
Remito Electrónico Cárnico.
<FECAEDetRequest><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc><Tipo>
10226
Si las actividades informadas 
corresponden a actividades Harinas, 
informar remito asociado 993 - 
Remito Electrónico Harinero - 
Automotor o 
994 - Remito Electrónico Harinero - 



# Página 69

Campo / Grupo Código 
de error Descripción de la validación
Ferroviario.
<FECAEDetRequest><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc><Tipo>
10227
Si las actividades informadas 
corresponden a actividades Tabaco 
en Hebras, informar remito asociado 
88 - Remito Electrónico de Tabaco 
Acondicionado.
<FECAEDetRequest><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc><Tipo>
10228
Si informa remito 995 - Remito 
Electrónico Cárnico, es obligatorio 
informar una actividad Cárnica.
<FECAEDetRequest><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc><Tipo>
10229
Si informa remito 993 - Remito 
Electrónico Harinero - Automotor o 
994 - Remito Electrónico Harinero – 
Ferroviario, es obligatorio informar 
una actividad Harinera.
<FECAEDetRequest><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc><Tipo>
10230
Si informa remito 88 - Remito 
Electrónico de Tabaco 
Acondicionado., es obligatorio 
informar una actividad 
correspondiente a Tabaco 
Acondicionado.
<FECAEDetRequest><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc
10231
Si informa actividades indicadas en 
la RG, es obligatorio informar 
comprobantes asociados.
<CbteAsoc><Tipo> / <CbteAsoc><PtoVta> / 
<CbteAsoc><Nro> 10232
Si informa comprobantes asociados, 
y sus códigos corresponden a 91, 88, 
988, 990, 991, 993, 994, 995, 996, 
997, los mismos no deben 
encontrarse asociado a otro 
comprobante.
<FECAEDetRequest> / <CanMisMonExt> 10239
Si informa el campo CanMisMonExt, 
los valores posibles son S o N y no 
debe quedar vacio.
<FECAEDetRequest> / <CanMisMonExt> 10240
Si informa el campo MonCotiz, el 
mismo no podra superar en 1 a la 
cotizacion oficial. Ver Metodo 
FEParamGetCotizacion.
<FECAEDetRequest> / <CanMisMonExt> 10241
Si informa MonId = PES, el campo 
CanMisMonExt no debe informarse 
o informarse con el valor N.
<FECAEDetRequest> / <CondicionIVAReceptorId> 10242
El campo de identificación de la 
Condición de IVA del receptor no es 
un valor permitido. Para mayor 



# Página 70

Campo / Grupo Código 
de error Descripción de la validación
detalle consular el método 
FEParamGetCondicionIvaReceptor.
<FECAEDetRequest> / <CondicionIVAReceptorId> 10243
El campo de identificación de 
Condición de IVA del receptor no es 
valido para la clase de comprobante 
informado. Para mas detalle 
consultar el Método: 
FEParamGetCondicionIvaReceptor 
<FECAEDetRequest> / <CondicionIVAReceptorId> 10246
Campo Condición Frente al IVA del 
receptor es obligatorio conforme a 
lo reglamentado por la Resolución 
General N° 5616. Para mas 
información consular método 
FEParamGetCondicionIvaReceptor.
Validaciones No Excluyentes
Campo / Grupo Código 
de 
Observ.
Descripción de la validación
<CbteTipo> / <DocNro>
10017
El campo DocNro para comprobantes 
Tipo A y M deberá ser un valor 
registrado en el padrón de arca, en 
condición activa.
<CbteAsoc><Tipo> / <CbteAsoc><PtoVta> / 
<CbteAsoc><Nro>
10041
Si el punto de venta del comprobante 
asociado (campo <PtoVta> de 
<CbtesAsoc>) es electrónico, el 
número de comprobante debe obrar 
en las bases del organismo para el 
punto de venta y tipo de 
comprobante informado.
DocTipo / DocNro
10063
Para comprobantes Clase A y M el 
receptor del comprobante informado 
en <DocTipo> y  <DocNro> debe 
corresponder a un contribuyente 
activo en el Impuesto al Valor 
Agregado o Responsable 
Monotributo.
<Auth><Cuit>/
<DocTipo>/ 10188
Si el tipo de comprobante que está 
autorizando es 1 - Factura A o 4 - 
Recibo A o 6 - Factura B o 9 - Recibo B 
o 11 - Factura C o 15 - Recibo C, por la 



# Página 71

Campo / Grupo Código 
de 
Observ.
Descripción de la validación
<DocNro>/ categorización de las cuits emisora y 
receptora,  se deberia realizar una 
factura de crédito electrónica.
<FECAEDetRequest><Tributos><Id>/
<FECAEDetRequest><PeriodoAsoc><FchDesde>/
<FECAEDetRequest><PeriodoAsoc><FchHasta>
10209
Si en la estructura Tributos informa 
percepciones, 
<PeriodoAsoc>.<FchDesde> y 
<PeriodoAsoc>.<FchHasta> deben 
corresponder al mismo Mes/Anio
<CbteTipo> / <DocNro>
10217
Para comprobantes Clase A y M, 
donde el receptor del comprobante 
informado en <DocTipo> y  <DocNro> 
se encuentra activo en el Impuesto 
Responsable Monotributo, “El crédito 
fiscal discriminado en el presente 
comprobante solo podrá ser 
computado a efectos del 
Procedimiento permanente de 
transición al Régimen General.” 
<CbteTipo> / <DocNro>
10234
Para comprobantes Clase A y M, se ha 
detectado que esta pendiente de 
presentación el formulario de 
habilitación de comprobantes o su 
fecha de presentación es anterior a la 
fecha de alta en IVA. Para el caso que 
el comprobante no sea una Nota de 
Crédito, se debe proceder a anular la 
operación clase "A" o "M" emitida, 
mediante una Nota de Crédito.
<CbteTipo> / <DocNro> / <FECAEDetRequest> / 
ImpTotal
10235
El monto total del comprobante 
emitido, excede el límite establecido 
para la categoría máxima de 
Monotributo. Por tal motivo, 
quedarías excluido automáticamente 
teniendo que solicitar el alta de los 
tributos (impositivos y de los recursos 
de la seguridad social) en el régimen 
general de acuerdo con tu actividad.
<CbteTipo> / <DocNro> / <FECAEDetRequest> / 
ImpTotal
10236
El monto del comprobante emitido, 
excede el límite establecido para su 
categoría de Monotributo. Tenelo en 
cuenta para la próxima 
recategorización.
<FECAEDetRequest> / ImpTotal / <CbteAsoc> 10237 El importe de la nota de crédito 



# Página 72

Campo / Grupo Código 
de 
Observ.
Descripción de la validación
supera el monto del comprobante 
asociado que estás ajustando. Verificá 
los montos ingresados y de tratarse 
de un error, tenés que efectuar el 
ajuste o anulación de la operación 
según corresponda.
<FECAEDetRequest> / <DocNro> 10238 La CUIT receptora ingresada no existe.
<FECAEDetRequest> / <CondicionIVAReceptorId>
10245
El campo Condición Frente al IVA del 
receptor resultará obligatorio 
conforme lo reglamentado por la 
Resolución General N° 5616. Para mas 
información consular método 
FEParamGetCondicionIvaReceptor.
Operatoria ante errores
Para la  operatoria del  método FECAESolicitar se describe la metodología sugerida  
ante rechazos / errores de los requerimiento  con múltiples comprobantes. 
Suponiendo  que se envían 100 comprobantes por  request y  el  mismo  es de Factu -
ras A, punto  de venta 1 y los comprobantes son del  51 al  150, se nos plantean 3 si -
tuaciones.
Aprobación total: donde, cada uno de  los 100  comprobantes fue aprobado
Rechazo  total: se puede dar por  dos causas, una por problemas del emisor, y otra  
por el  rechazo del primer comprobante enviado en el bloque de comprobantes del de -
talle. En el primer caso el response contendrá en  el  tag Errors todas las causas invo -
lucradas; en el  segundo  caso se incluirá el  tag Obs con el  motivo de rechazo u ob -
servación de los comprobantes.
Rechazo parcial: se da cuando alguno de los comprobantes incluidos en el  request es  
rechazado. A modo  de ejemplo y con los parámetros antes descriptos,  se aprueban  
los comprobantes del  51 al 100, 101 saldrá rechazado y del 102 al  150 saldrá como  
no procesado; esto se debe a que como debe existir correlatividad numérica y fecha,  
ante una inconsistencia los comprobantes subsiguientes también se rechazaran. Si se  
diese este caso, y para proseguir con la autorización de comprobantes se deberá sub -
sanar los errores del  102 y así  enviar un nuevo  request.
Operatoria con errores de comunicación: 
En el diseño del WsfeV1 se ha previsto que –dada la complejidad actual de las comu -
nicaciones- pueden ocurrir interrupciones en la comunicación entre el cliente y el Ws -
feV1 básicamente, el problema podría resumirse al siguiente escenario: el cliente en -



# Página 73

vía una solicitud de CAE al WsfeV1 y se queda esperando una respuesta que no llega,  
hasta que transcurrido algún tiempo, se produce una condición de time-out. 
En ese caso, el usuario no sabrá si la solicitud le llegó al WsfeV1, este asignó el CAE y 
la falla de comunicación se produjo durante el retorno de la información, o bien si la fa -
lla ocurrió durante el envío de la solicitud y simplemente WsfeV1 nunca la recibió.
En el segundo caso, con simplemente enviar la misma solicitud todo quedaría resuelto, 
pero en el primer caso, si el cliente envía la misma nueva solicitud de CAE para la mis-
ma factura, WsfeV1 devolvería un error de consecutividad puesto que en la base de  
datos de arca esa factura ya figura como emitida.
Para estos casos, se utiliza el  método FECompConsultar, que dado  el tipo de com -
probante, punto  de venta y numero  de comprobante, retorna toda la información en -
viada en  el  método  de autorización (FECAESolicitar)  más el  CAE, fecha de venci -
miento  del  mismo. El WsfeV1 también ofrece mecanismo para la consulta del  último  
comprobante autorizado (FECompUltimoAutorizado).
Ejemplos 
Ejemplo  1  -  Factura A con diferentes Alícuotas de IVA y Tributos sin errores
Esquema de factura 
FEDetRequest
         
 Emisor Tipo Factura A Pto Vta / Nro 0012-00000001  
 Fecha : 03 – Sep -2010  
 Sr Cliente (destinatario)  
  
  $ neto % IVA $ IVA $ totales  
 item1 100,00 21,0% 21,00 121  
 item2 50,00 10,5% 5,25 52,25  
 <Tributos>  
Tributos Base % Importe
Base imponible 150,00 5,2 7,8
  
 Totales  $ 150,00  $ 26,25  $   7,80  $ 184,05  
 <ImpNeto> <ImpIVA> <ImpTrib> <ImpTotal>  
  
 Moneda PES  
 Tipo  de cambio 1  
         
REQUEST



# Página 74

<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
       <ar:FECAESolicitar>
        <!--Optional:-->
         <ar:Auth> 
            <ar:Token>PD94…..</ar:Token> 
            <ar:Sign>tYft0….....</ar:Sign>
            <ar:Cuit>33693450239</ar:Cuit>
         </ar:Auth> 
         <ar:FeCAEReq> 
            <ar:FeCabReq>
               <ar:CantReg>1</ar:CantReg>
               <ar:PtoVta>12</ar:PtoVta> 
               <ar:CbteTipo>1</ar:CbteTipo>  FACTURA A
            </ar:FeCabReq>
            <ar:FeDetReq> 
             <ar:FECAEDetRequest>
                  <ar:Concepto>1</ar:Concepto>  Productos
                  <ar:DocTipo>80</ar:DocTipo>  CUIT
                  <ar:DocNro>20111111112</ar:DocNro>
                  <ar:CbteDesde>1</ar:CbteDesde>
                  <ar:CbteHasta>1</ar:CbteHasta> 
                  <ar:CbteFch>20100903</ar:CbteFch>
                  <ar:ImpTotal>184.05</ar:ImpTotal> 
                  <ar:ImpTotConc>0</ar:ImpTotConc>
                  <ar:ImpNeto>150</ar:ImpNeto> 
                  <ar:ImpOpEx>0</ar:ImpOpEx>
                  <ar:ImpTrib>7.8</ar:ImpTrib>
                  <ar:ImpIVA>26.25</ar:ImpIVA>
                  <ar:FchServDesde></ar:FchServDesde> 
                  <ar:FchServHasta></ar:FchServHasta> 
                  <ar:FchVtoPago></ar:FchVtoPago> 
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:Tributos> 
                     <ar:Tributo>
                        <ar:Id>99</ar:Id> 
                        <ar:Desc>Impuesto Municipal Matanza</ar:Desc>
                        <ar:BaseImp>150</ar:BaseImp>
                        <ar:Alic>5.2</ar:Alic>
                        <ar:Importe>7.8</ar:Importe>
                     </ar:Tributo>
                  </ar:Tributos> 
                  <ar:Iva>
                     <ar:AlicIva>
                        <ar:Id>5</ar:Id>  21%
                        <ar:BaseImp>100</ar:BaseImp>
                        <ar:Importe>21</ar:Importe>
                     </ar:AlicIva>
                     <ar:AlicIva>
                        <ar:Id>4</ar:Id>  10.5%
                        <ar:BaseImp>50</ar:BaseImp>
                        <ar:Importe>5.25</ar:Importe>



# Página 75

                     </ar:AlicIva>
                  </ar:Iva> 
</ar:FECAEDetRequest>
            </ar:FeDetReq>
         </ar:FeCAEReq>
      </ar:FECAESolicitar>
   </soapenv:Body>
</soapenv:Envelope> 
RESPONSE
<soap:Envelope xmlns:soap=”http://www.w3.org/2003/05/soap-envelope” 
xmlns:ar=”http://ar.gov.afip.dif.fev1/”>
   <soap:Header/>
   <soap:Body>
      <FECAESolicitarResponse>
         <FECAESolicitarResult>
            <FeCabResp>
               <PtoVta>12</PtoVta> 
               <CbteTipo>1</CbteTipo>  FACTURA A
               <FchProceso>20100902</FchProceso>
               <CantReg>1</CantReg>
               <Resultado>A</Resultado>  A=APROBADO, R=RECHAZADO, 
P=PARCIAL
<Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp> 
               <FECAEDetResponse>
                  <Concepto>1</Concepto> 
                  <DocTipo>80</DocTipo>  CUIT
                  <DocNro>20111111112</DocNro>
                  <CbteDesde>1</CbteDesde>
                  <CbteHasta>1</CbteHasta>
                  <CbteFch>20100903</CbteFch> 
                  <Resultado>A</Resultado> 
                  <CAE>41124578989845</CAE> 
                  <CAEFchVto>20100913</CAEFchVto>
               </FECAEDetResponse>
            </FeDetResp> 
         </FECAESolicitarResult>
      </FECAESolicitarResponse>
   </soap:Body>
</soap:Envelope>
Ejemplo  2  -  Envío  con 2  Facturas A con errores parciales (a nivel  FEDetRequest). 
REQUEST
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAESolicitar>
        <!--Optional:-->



# Página 76

         <ar:Auth> 
            <ar:Token>PD94…..</ar:Token> 
            <ar:Sign>tYft0….....</ar:Sign>
            <ar:Cuit>33693450239</ar:Cuit>
         </ar:Auth> 
         <ar:FeCAEReq> 
            <ar:FeCabReq>
               <ar:CantReg>2</ar:CantReg> 
               <ar:PtoVta>12</ar:PtoVta> 
               <ar:CbteTipo>1</ar:CbteTipo>  FACTURA A
            </ar:FeCabReq>
            <ar:FeDetReq> 
               <ar:FECAEDetRequest>
                  <ar:Concepto>1</ar:Concepto>  Productos
                  <ar:DocTipo>80</ar:DocTipo>  CUIT
                  <ar:DocNro>20111111112</ar:DocNro>
                  <ar:CbteDesde>2</ar:CbteDesde>
                  <ar:CbteHasta>2</ar:CbteHasta> 
                  <ar:CbteFch>20100903</ar:CbteFch>
                  <ar:ImpTotal>184.05</ar:ImpTotal> 
                  <ar:ImpTotConc>0</ar:ImpTotConc>
                  <ar:ImpNeto>150</ar:ImpNeto> 
                  <ar:ImpOpEx>0</ar:ImpOpEx>
                  <ar:ImpTrib>7.8</ar:ImpTrib>
                  <ar:ImpIVA>26.25</ar:ImpIVA>
                  <ar:FchServDesde></ar:FchServDesde> 
                  <ar:FchServHasta></ar:FchServHasta> 
                  <ar:FchVtoPago></ar:FchVtoPago> 
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:Tributos> 
                     <ar:Tributo>
                        <ar:Id>99</ar:Id> 
                        <ar:Desc>Impuesto Municipal Matanza</ar:Desc>
                        <ar:BaseImp>150</ar:BaseImp>
                        <ar:Alic>5.2</ar:Alic>
                        <ar:Importe>7.8</ar:Importe>
                     </ar:Tributo>
                  </ar:Tributos> 
                  <ar:Iva>
                     <ar:AlicIva>
                        <ar:Id>5</ar:Id>  21%
                        <ar:BaseImp>100</ar:BaseImp>
                        <ar:Importe>21</ar:Importe>
                     </ar:AlicIva>
                     <ar:AlicIva>
                        <ar:Id>4</ar:Id>  10.5%
                        <ar:BaseImp>50</ar:BaseImp>
                        <ar:Importe>5.25</ar:Importe>
                    </ar:AlicIva>
        </ar:Iva>
</ar:FECAEDetRequest>
<ar:FECAEDetRequest> 
                <ar:Concepto>1</ar:Concepto>  Productos
                  <ar:DocTipo>80</ar:DocTipo>    CUIT



# Página 77

                  <ar:DocNro>10222222222</ar:DocNro>  no existente en 
Padrón arca
                  <ar:CbteDesde>4</ar:CbteDesde>
                  <ar:CbteHasta>4</ar:CbteHasta> 
                  <ar:CbteFch>20100901</ar:CbteFch>
                  <ar:ImpTotal>184.05</ar:ImpTotal> 
                  <ar:ImpTotConc>0</ar:ImpTotConc>
                  <ar:ImpNeto>150</ar:ImpNeto> 
                  <ar:ImpOpEx>0</ar:ImpOpEx>
                  <ar:ImpTrib>7.8</ar:ImpTrib>
                  <ar:ImpIVA>26.25</ar:ImpIVA>
                  <ar:FchServDesde></ar:FchServDesde> 
                  <ar:FchServHasta></ar:FchServHasta> 
                  <ar:FchVtoPago></ar:FchVtoPago> 
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:Tributos> 
                     <ar:Tributo>
                        <ar:Id>99</ar:Id> 
                        <ar:Desc>Impuesto Municipal Matanza</ar:Desc>
                        <ar:BaseImp>150</ar:BaseImp>
                        <ar:Alic>5.2</ar:Alic>
                        <ar:Importe>7.8</ar:Importe>
                     </ar:Tributo>
                  </ar:Tributos> 
                  <ar:Iva>
                     <ar:AlicIva>
                        <ar:Id>5</ar:Id>  21%
                        <ar:BaseImp>100</ar:BaseImp>
                        <ar:Importe>21</ar:Importe>
                     </ar:AlicIva>
                     <ar:AlicIva>
                        <ar:Id>4</ar:Id>  10.5%
                        <ar:BaseImp>50</ar:BaseImp>
                        <ar:Importe>5.25</ar:Importe>
                     </ar:AlicIva>
                  </ar:Iva>
</ar:FECAEDetRequest>
               </ar:FeDetReq>
         </ar:FeCAEReq>
      </ar:FECAESolicitar>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap=”http://www.w3.org/2003/05/soap-envelope” 
xmlns:ar=”http://ar.gov.afip.dif.fev1/”>
   <soap:Header/>
   <soap:Body>
      <FECAESolicitarResponse>
         <FECAESolicitarResult>
            <FeCabResp>
               <CantReg>2</CantReg>



# Página 78

               <PtoVta>12</PtoVta> 
               <CbteTipo>1</CbteTipo>  FACTURA A
               <FchProceso>20100902</FchProceso>
               <Resultado>P</Resultado>  A=APROBADO, R=RECHAZADO, 
P=PARCIAL
<Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp> 
               <FECAEDetResponse>
                  <Concepto>1</Concepto> 
                  <DocTipo>80</DocTipo>  CUIT
                  <DocNro>20111111112</DocNro>
                  <CbteDesde>2</CbteDesde>
                  <CbteHasta>2</CbteHasta>
                  <CbteFch>20100903</CbteFch> 
                  <Resultado>A</Resultado> 
                  <CAE>41124599989845</CAE> 
                  <CAEFchVto>20100913</CAEFchVto>
               </FECAEDetResponse>
               <FECAEDetResponse>
                  <Concepto>1</Concepto> 
                  <DocTipo>80</DocTipo>  CUIT
                  <DocNro>10222222222</DocNro> 
                  <CbteDesde>4</CbteDesde>
                  <CbteHasta>4</CbteHasta>
                  <CbteFch>20100901</CbteFch> 
                  <Resultado>R</Resultado> 
                  <CAE></CAE>                    Sin CAE por  Rechazo
                  <CAEFchVto></CAEFchVto>
                  <Observaciones> 
                     <Obs>
                        <Code>10030</Code> 
                        <Msg>Cuit 10222222222 no registrada en  padrón 
arca</Msg>
                     </Obs>
                     <Obs>
                        <Code>10016</Code> 
                        <Msg>comp. 4 no coincide con el  próximo a au-
torizar</Msg>
                     </Obs>
                  </Observaciones>
               </FECAEDetResponse>
            </FeDetResp> 
         </FECAESolicitarResult>
      </FECAESolicitarResponse>
   </soap:Body>
</soap:Envelope>
Ejemplo  3  -  Envio  con 1  Facturas A con errores generales (a nivel de FeCAEReq).
REQUEST



# Página 79

<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAESolicitar>
        <!--Optional:-->
         <ar:Auth> 
            <ar:Token>PD94…..</ar:Token> 
            <ar:Sign>tYft0….....</ar:Sign>
            <ar:Cuit>33693450239</ar:Cuit>
         </ar:Auth> 
         <ar:FeCAEReq> 
            <ar:FeCabReq>
               <ar:CantReg>2</ar:CantReg> 
               <ar:PtoVta>15</ar:PtoVta> 
               <ar:CbteTipo>1</ar:CbteTipo>  FACTURA A
            </ar:FeCabReq>
            <ar:FeDetReq> 
                <ar:FECAEDetRequest> 
               <ar:Concepto>1</ar:Concepto>  Productos
                  <ar:DocTipo>80</ar:DocTipo>  CUIT
                  <ar:DocNro>20111111112</ar:DocNro>
                  <ar:CbteDesde>1</ar:CbteDesde>
                  <ar:CbteHasta>1</ar:CbteHasta> 
                  <ar:CbteFch>20100903</ar:CbteFch>
                  <ar:ImpTotal>184.05</ar:ImpTotal> 
                  <ar:ImpTotConc>0</ar:ImpTotConc>
                  <ar:ImpNeto>150</ar:ImpNeto> 
                  <ar:ImpOpEx>0</ar:ImpOpEx>
                  <ar:ImpTrib>7.8</ar:ImpTrib>
                  <ar:ImpIVA>26.25</ar:ImpIVA>
                  <ar:FchServDesde></ar:FchServDesde> 
                  <ar:FchServHasta></ar:FchServHasta> 
                  <ar:FchVtoPago></ar:FchVtoPago> 
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:Tributos> 
                     <ar:Tributo>
                        <ar:Id>99</ar:Id> 
                        <ar:Desc>Impuesto Municipal Matanza</ar:Desc>
                        <ar:BaseImp>150</ar:BaseImp>
                        <ar:Alic>5.2</ar:Alic>
                        <ar:Importe>7.8</ar:Importe>
                     </ar:Tributo>
                  </ar:Tributos> 
                  <ar:Iva>
                     <ar:AlicIva>
                        <ar:Id>5</ar:Id>  21%
                        <ar:BaseImp>100</ar:BaseImp>
                        <ar:Importe>21</ar:Importe>
                     </ar:AlicIva>
                     <ar:AlicIva>
                        <ar:Id>4</ar:Id>  10.5%
                        <ar:BaseImp>50</ar:BaseImp>
                        <ar:Importe>5.25</ar:Importe>



# Página 80

                     </ar:AlicIva>
                  </ar:Iva>
                </ar:FECAEDetRequest>
            </ar:FeDetReq>
         </ar:FeCAEReq>
      </ar:FECAESolicitar>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap=”http://www.w3.org/2003/05/soap-envelope” 
xmlns:ar=”http://ar.gov.afip.dif.fev1/”>
   <soap:Header/>
   <soap:Body>
      <FECAESolicitarResponse>
         <FECAESolicitarResult>
            <FeCabResp>
               <CantReg>2</CantReg>
               <PtoVta>15</PtoVta> 
               <CbteTipo>1</CbteTipo>  FACTURA A
               <FchProceso>20100902</FchProceso>
               <Resultado>R</Resultado>  A=APROBADO, R=RECHAZADO, 
P=PARCIAL
<Reproceso>N</Reproceso>
            </FeCabResp>
            <Errors>
               <Err>
                  <Code>10002</Code> 
                  <Msg>No coincide la cantidad de registros informadas con la cantidad real en-
viada</Msg>
               </Err>
               <Err>
                  <Code>1005</Code> 
                  <Msg>El punto  de venta no se encuentra empadrona-
do</Msg>
               </Err>
            </Errors>
         </FECAESolicitarResult>
      </FECAESolicitarResponse>
   </soap:Body>
</soap:Envelope>
Ejemplo  4  -  Envio  con 1  Factura Tipo 49 – Bienes Usados para emisor RI con errores  (a nivel 
de FeCAEReq).
REQUEST
<soapenv:Envelope 
xmlns:soapenv=”http://schemas.xmlsoap.org/soap/envelope/” 
xmlns:ar=”http://ar.gov.afip.dif.FEV1/”>
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAESolicitar>
         <ar:Auth> 
            <ar:Token>PD94…..</ar:Token> 



# Página 81

            <ar:Sign>tYft0….....</ar:Sign>
            <ar:Cuit>23000000004</ar:Cuit>
         </ar:Auth> 
         <ar:FeCAEReq> 
            <ar:FeCabReq>
               <ar:CantReg>1</ar:CantReg> 
               <ar:PtoVta>15</ar:PtoVta> 
               <ar:CbteTipo>49</ar:CbteTipo>  BIENES USADOS
            </ar:FeCabReq>
            <ar:FeDetReq> 
               <ar:FECAEDetRequest>
                  <ar:Concepto>2</ar:Concepto>  Servicios
                  <ar:DocTipo>80</ar:DocTipo>  CUIT
                  <ar:DocNro>20111111112</ar:DocNro>
                  <ar:CbteDesde>1</ar:CbteDesde>
                  <ar:CbteHasta>1</ar:CbteHasta> 
                  <ar:CbteFch>20130708</ar:CbteFch>
                  <ar:ImpTotal>1605</ar:ImpTotal> 
                  <ar:ImpTotConc>1000</ar:ImpTotConc>
                  <ar:ImpNeto>500</ar:ImpNeto> 
                  <ar:ImpOpEx>0</ar:ImpOpEx>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:ImpIVA>105</ar:ImpIVA>
                  <ar:FchServDesde>20130708</ar:FchServDesde> 
                  <ar:FchServHasta>20130708</ar:FchServHasta> 
                  <ar:FchVtoPago>20130708</ar:FchVtoPago> 
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:Iva>
                     <ar:AlicIva>
                        <ar:Id>5</ar:Id>  21%
                        <ar:BaseImp>500</ar:BaseImp>
                        <ar:Importe>105</ar:Importe>
                     </ar:AlicIva>
                  </ar:Iva>
               </ar:FECAEDetRequest>
            </ar:FeDetReq>
         </ar:FeCAEReq>
      </ar:FECAESolicitar>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap=”http://www.w3.org/2003/05/soap-envelope” 
xmlns:ar=”http://ar.gov.afip.dif.FEV1/”>
   <soap:Header/>
   <soap:Body>
      <FECAESolicitarResponse>
         <FECAESolicitarResult>
            <FeCabResp>
               <Cuit>23000000004</Cuit>
               <PtoVta>15</PtoVta>
               <CbteTipo>49</CbteTipo>
               <FchProceso>20130708124213</FchProceso>



# Página 82

               <CantReg>1</CantReg>
               <Resultado>R</Resultado>
               <Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp>
               <FECAEDetResponse>
                  <Concepto>2</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>1</CbteDesde>
                  <CbteHasta>1</CbteHasta>
                  <CbteFch>20130708</CbteFch>
                  <Resultado>R</Resultado>
                  <Observaciones>
                     <Obs>
                        <Code>10030</Code>
                        <Msg>Para comprobantes de Bienes Usados, Con-
cepto debe ser igual a 1 – PRODUCTOS</Msg>
                     </Obs>
                     <Obs>
                        <Code>10076</Code>
                        <Msg>Si el comprobante es CbteTipo = 49 
(Bienes Usados), es obligatorio informar opcionales. Ver método FEPa-
ramGetTiposOpcional()</Msg>
                     </Obs>
                  </Observaciones>
                  <CAE/>
                  <CAEFchVto/>
               </FECAEDetResponse>
            </FeDetResp>
         </FECAESolicitarResult>
      </FECAESolicitarResponse>
   </soap:Body>
</soap:Envelope> 
Ejemplo  5  -  Envio  con 1  Factura Tipo 49 – Bienes Usados para emisor RI.
REQUEST
<soapenv:Envelope 
xmlns:soapenv=”http://schemas.xmlsoap.org/soap/envelope/” 
xmlns:ar=”http://ar.gov.afip.dif.FEV1/”>
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAESolicitar>
          <!--Optional:-->
          <ar:Auth>
             <ar:Token>PD94…..</ar:Token>
             <ar:Sign>tYft0….....</ar:Sign>
            <ar:Cuit>23000000004</ar:Cuit>
         </ar:Auth> 
            <!--Optional:-->
         <ar:FeCAEReq>
         <!--Optional:-->
            <ar:FeCabReq>



# Página 83

               <ar:CantReg>1</ar:CantReg>
               <ar:PtoVta>1114</ar:PtoVta>
               <ar:CbteTipo>49</ar:CbteTipo>
            </ar:FeCabReq>
             <!--Optional:-->
            <ar:FeDetReq>
                <ar:FECAEDetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>6</ar:CbteDesde>
                  <ar:CbteHasta>6</ar:CbteHasta>
                  <!–Optional:
                  <ar:CbteFch>20130720</ar:CbteFch>
                  <ar:ImpTotal>1605</ar:ImpTotal>
                  <ar:ImpTotConc>1000</ar:ImpTotConc>
                  <ar:ImpNeto>500</ar:ImpNeto>
                  <ar:ImpOpEx>0</ar:ImpOpEx>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:ImpIVA>105</ar:ImpIVA>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:Iva>
                     <ar:AlicIva>
                        <ar:Id>5</ar:Id>
                        <ar:BaseImp>500</ar:BaseImp>
                        <ar:Importe>105</ar:Importe>
                     </ar:AlicIva>
                   </ar:Iva>
                  <ar:Opcionales>
                   <ar:Opcional>
                        <ar:Id>91</ar:Id>
                        <ar:Valor>83Nerina Soledad Estela</ar:Valor>
                     </ar:Opcional>
                     <ar:Opcional>
                        <ar:Id>93</ar:Id>
                        <ar:Valor>Libertad 3333 Torre A – CABA – Ar-
gentina</ar:Valor>
                     </ar:Opcional>
                  </ar:Opcionales>
                </ar:FECAEDetRequest>
            </ar:FeDetReq>
         </ar:FeCAEReq>
      </ar:FECAESolicitar>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/” 
xmlns:xsi=”http://www.w3.org/2001/XMLSchema-instance” 
xmlns:xsd=”http://www.w3.org/2001/XMLSchema”>
   <soap:Body>
      <FECAESolicitarResponse xmlns=”http://ar.gov.afip.dif.FEV1/”>
         <FECAESolicitarResult>



# Página 84

            <FeCabResp>
               <Cuit>23000000004</Cuit>
               <PtoVta>1114</PtoVta>
               <CbteTipo>49</CbteTipo>
               <FchProceso>20130715114927</FchProceso>
               <CantReg>1</CantReg>
               <Resultado>A</Resultado>
               <Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp>
               <FECAEDetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>5</CbteDesde>
                  <CbteHasta>5</CbteHasta>
                  <CbteFch>20130720</CbteFch>
                  <Resultado>A</Resultado>
                  <CAE>63288001286615</CAE>
                  <CAEFchVto>20130730</CAEFchVto>
               </FECAEDetResponse>
            </FeDetResp>
         </FECAESolicitarResult>
      </FECAESolicitarResponse>
   </soap:Body>
</soap:Envelope>
Ejemplo  6  -  Envio  con 1  Factura Tipo 49 – Bienes Usados para emisor Monotributista.
REQUEST
<soapenv:Envelope 
xmlns:soapenv=”http://schemas.xmlsoap.org/soap/envelope/” 
xmlns:ar=”http://ar.gov.afip.dif.FEV1/”>
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAESolicitar>
       <!--Optional:-->
          <ar:Auth>
            <ar:Token>PD94…..</ar:Token>
             <ar:Sign>tYft0….....</ar:Sign>
            <ar:Cuit>23000000004</ar:Cuit>
         </ar:Auth> 
             <!--Optional:-->
         <ar:FeCAEReq>
              <!--Optional:--> 
         <ar:FeCabReq>
               <ar:CantReg>1</ar:CantReg>
               <ar:PtoVta>2</ar:PtoVta>
               <ar:CbteTipo>49</ar:CbteTipo>
            </ar:FeCabReq>
              <!--Optional:-->
            <ar:FeDetReq>
               <!–-Zero or more repetitions:-->
               <ar:FECAEDetRequest>



# Página 85

                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>91</ar:DocTipo>
                  <ar:DocNro>11111111111</ar:DocNro>
                  <ar:CbteDesde>1</ar:CbteDesde>
                  <ar:CbteHasta>1</ar:CbteHasta>
                    <!--Optional:-->
                  <ar:CbteFch>20130715</ar:CbteFch>
                  <ar:ImpTotal>1500</ar:ImpTotal>
                  <ar:ImpTotConc>1000</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>0</ar:ImpOpEx>
                  <ar:ImpTrib>500</ar:ImpTrib>
                  <ar:ImpIVA>0</ar:ImpIVA>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                 <ar:Tributos>
                     <ar:Tributo>
                        <ar:Id>1</ar:Id>
                        <ar:Desc>2</ar:Desc>
                        <ar:BaseImp>1000</ar:BaseImp>
                        <ar:Alic>25</ar:Alic>
                        <ar:Importe>250</ar:Importe>
                     </ar:Tributo>
                     <ar:Tributo>
                        <ar:Id>2</ar:Id>
                        <ar:Desc>2</ar:Desc>
                        <ar:BaseImp>500</ar:BaseImp>
                        <ar:Alic>100</ar:Alic>
                        <ar:Importe>250</ar:Importe>
                     </ar:Tributo>
                  </ar:Tributos>
                  <ar:Opcionales>
                   <ar:Opcional>
                        <ar:Id>91</ar:Id>
                        <ar:Valor>Atilio Raúl Butaraco</ar:Valor>
                     </ar:Opcional>
                    <ar:Opcional>
                        <ar:Id>92</ar:Id>
                        <ar:Valor>225</ar:Valor>
                     </ar:Opcional>
                     <ar:Opcional>
                        <ar:Id>93</ar:Id>
                        <ar:Valor>Av. Benito Blanco 50 piso 3 – Cuidad 
de Montevideo – Uruguay</ar:Valor>
                   </ar:Opcional>
                  </ar:Opcionales>
                </ar:FECAEDetRequest>
            </ar:FeDetReq>
         </ar:FeCAEReq>
      </ar:FECAESolicitar>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE



# Página 86

<soap:Envelope xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/” 
xmlns:xsi=”http://www.w3.org/2001/XMLSchema-instance” 
xmlns:xsd=”http://www.w3.org/2001/XMLSchema”>
   <soap:Body>
      <FECAESolicitarResponse xmlns=”http://ar.gov.afip.dif.FEV1/”>
         <FECAESolicitarResult>
            <FeCabResp>
               <Cuit>23000000004</Cuit>
               <PtoVta>2</PtoVta>
               <CbteTipo>49</CbteTipo>
               <FchProceso>20130715130307</FchProceso>
               <CantReg>1</CantReg>
               <Resultado>A</Resultado>
               <Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp>
               <FECAEDetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>91</DocTipo>
                  <DocNro>11111111111</DocNro>
                  <CbteDesde>1</CbteDesde>
                  <CbteHasta>1</CbteHasta>
                  <CbteFch>20130715</CbteFch>
                  <Resultado>A</Resultado>
                  <CAE>63288001286628</CAE>
                  <CAEFchVto>20130725</CAEFchVto>
               </FECAEDetResponse>
            </FeDetResp>
         </FECAESolicitarResult>
      </FECAESolicitarResponse>
   </soap:Body>
</soap:Envelope>
Método  de obtención de CAEA (FECAEASolicitar)
Esta operación permite solicitar un CAEA. El cliente envía el requerimiento, el cual es atendido 
por el WS, superadas las validaciones se otorgará un CAEA y su respectivo periodo de vigencia 
(fecha de validez desde y fecha de validez hasta).
Podrá ser solicitado dentro de cada quincena y hasta  5 (cinco) días corridos anteriores al 
comienzo de cada quincena. Habrá dos quincenas, la primera abarca desde el primero hasta el 
quince de cada mes y la segunda desde el dieciséis hasta el último día del mes.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= FECAEASolicitar
Mensaje de solicitud
<soapenv:Envelope 



# Página 87

xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEASolicitar>
           <ar:Auth>
            <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
         <ar:Periodo>int</ar:Periodo>
         <ar:Orden>short</ar:Orden>
      </ar:FECAEASolicitar>
   </soapenv:Body>
</soapenv:Envelope>
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign y Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Campo Detalle Obligatorio
FeCAEAReq Información del periodo y orden del CAEA que se está 
solicitando S
FeCAEAReq:
Campo Tipo Detalle Obligatorio
Periodo Int (6) Periodo del CAEA.  (yyyymm) S
Orden Short (1) Orden del CAEA dentro del periodo. 
Quincena 1, Quincena 2 S
Mensaje de respuesta
Retorna los detalles de un CAEA autorizado.
<?xml version=”1.0” encoding=”utf-8”?>
<soap12:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap12=”http://www.w3.org/2003/05/soap-envelope”>
  <soap12:Body>
    <FECAEASolicitarResponse xmlns=”http://ar.gov.afip.dif.FEV1/”>



# Página 88

      <FECAEASolicitarResult>
        <ResultGet>
          <CAEA>string</CAEA>
          <Periodo>int</Periodo>
          <Orden>short</Orden>
          <FchVigDesde>string</FchVigDesde>
          <FchVigHasta>string</FchVigHasta>
          <FchTopeInf>string</FchTopeInf>
          <FchProceso>string</FchProceso>
          <Observaciones>
            <Obs>
               <Code>int</Code>
               <Msg>string</ Msg>
            </Obs>
          </Observaciones>
        </ResultGet>
        <Errors>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
        </Events>
      </FECAEASolicitarResult>
    </FECAEASolicitarResponse>
  </soapenv:Body>
</soap:Envelope>
Donde:
FECAEASolicitarResult:
Campo Detalle Obligatorio
ResultGet Información completa del  CAEA autorizado S
Errors Información de errores detectados N
Events Información de eventos N
ResultGet: está compuesto por los siguientes campos:
Campo Tipo Detalle Obligatorio



# Página 89

CAEA String (14) Código de Autorización electrónico anticipado N
Periodo Int (6) Periodo (yyyymm) S
Orden Short (1) Orden. Quincena 1, quincena 2 S
FchVigDesde String (8) Fecha de vigencia de CAEA desde N
FchVigHasta String (8) Fecha de vigencia de CAEA hasta N
FchTopeInf String (c8) Fecha de tope para informar los 
comprobantes vinculados al  CAEA N
FchProceso String (14) Fecha de proceso, formato yyyymmddhhmiss N
Observaciones Array Detalle de observaciones, del comprobante N
Observaciones: La estructura de datos Obs muestra el  detalle de observaciones para el CAEA 
generado; estará compuesta por los siguientes campos: 
Campo Tipo Detalle Obligatorio
Code Int (5) Código  de observación S
Msg String (255) Mensaje S
Validaciones y errores 
Controles aplicados al elemento <FeCAEAReq>
Validaciones Excluyentes
Campo / 
Grupo
Código de 
error Descripción de la validación
<Cuit> 15000 Campo  CUIT: Debe encontrarse activa en el Sistema Registral y 
activo en el Régimen para solicitar CAEA
<Cuit> 15001 Campo  CUIT: Deberá estar registrado como Autoimpresor
<Cuit> 15003 Campo  CUIT: Deberá poseer al menos un punto de venta activo 
correspondiente al régimen CAEA 
<Periodo> 15004 Campo  Periodo: Debe tener el formato AAAAMM, donde AAAA 
indica el año y MM el mes en números.
<Orden> 15005 Campo  Orden: Debe ser igual a 1 ó 2.
Fecha de envío 15006 Al momento de solicitar CAEA, la fecha de envío podrá ser desde 
5 (cinco) días corridos anteriores al inicio de cada quincena  hasta 



# Página 90

Campo / 
Grupo
Código de 
error Descripción de la validación
el último día de la misma quincena.
<Periodo> / 
<Orden> 15008 No debe existir un CAEA otorgado para la CUIT solicitante con 
igual periodo y orden.
<Cuit> 15009 Campo  CUIT: Registra problemas de domicilio 
<Cuit> 15010
Campo  CUIT: Deberá estar inscripto  en alguno de los sig. 
impuestos:
20 - MONOTRIBUTO 
30 - IVA 
32 - IVA EXENTO
<Cuit> 15011 Campo  CUIT: Deberá tener al menos una actividad económica 
declarada
<Cuit> 15012 Campo  CUIT: Deberá estar empadronado en  el  régimen de 
emisión de comprobantes electrónicos
Validaciones No Excluyentes
Campo / Grupo Código de 
Observ. Descripción de la validación
<Cuit>/
<Periodo> / <Orden>
15014
Que No adeude la presentación del Régimen por 2 periodos 
mensuales consecutivos ó 3 periodos mensuales alternados 
en los últimos 12 meses calendario.
<Cuit> 15015 Campo  CUIT: Registra problemas en el domicilio fiscal 
electrónico. No se encuentra adherido.
Ejemplo sin Observaciones:
Request
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEASolicitar>
         <!--Optional:-->



# Página 91

         <ar:Auth>
            <!--Optional:-->
            <ar:Token>un string</ar:Token>
             <ar:Sign>un string</ar:Sign>
            <ar:Cuit>33333333333</ar:Cuit>
         </ar:Auth>
         <ar:Periodo>201011</ar:Periodo>
         <ar:Orden>1</ar:Orden>
      </ar:FECAEASolicitar>
   </soapenv:Body>
</soapenv:Envelope>
Response
<?xml version=”1.0” encoding=”utf-8”?>
<soap12:Envelopexmlns:xsi=”http://www.w3.org/2001/
XMLSchema-instance” 
xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap12=”http://www.w3.org/2003/05/soap-envelope”>
  <soap12:Body>
    <FECAEASolicitarResponse 
xmlns=”http://ar.gov.afip.dif.FEV1/”>
      <FECAEASolicitarResult>
        <ResultGet>
          <CAEA>12345678901234</CAEA>
          <Periodo>201011</Periodo>
          <Orden>1</Orden>
          <FchVigDesde>20101101</FchVigDesde>
          <FchVigHasta>20101115</FchVigHasta>
          <FchTopeInf>20101215</FchTopeInf>
          <FchProceso>20101028</FchProceso>
        </ResultGet>
      </FECAEASolicitarResult>
    </FECAEASolicitarResponse>
  </soapenv:Body>
</soap:Envelope>
Ejemplo  con observaciones:
Request
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEASolicitar>



# Página 92

         <!--Optional:-->
         <ar:Auth>
            <!--Optional:-->
            <ar:Token>un string</ar:Token>
             <ar:Sign>un string</ar:Sign>
            <ar:Cuit>33333333333</ar:Cuit>
         </ar:Auth>
         <ar:Periodo>201011</ar:Periodo>
         <ar:Orden>1</ar:Orden>
      </ar:FECAEASolicitar>
   </soapenv:Body>
</soapenv:Envelope>
Response
<?xml version=”1.0” encoding=”utf-8”?>
<soap12:Envelopexmlns:xsi=”http://www.w3.org/2001/
XMLSchema-instance” 
xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap12=”http://www.w3.org/2003/05/soap-envelope”>
  <soap12:Body>
    <FECAEASolicitarResponse 
xmlns=”http://ar.gov.afip.dif.FEV1/”>
      <FECAEASolicitarResult>
        <ResultGet>
          <CAEA>12345678901234</CAEA>
          <Periodo>201011</Periodo>
          <Orden>1</Orden>
          <FchVigDesde>20101101</FchVigDesde>
          <FchVigHasta>20101115</FchVigHasta>
          <FchTopeInf>20101215</FchTopeInf>
          <FchProceso>20101028</FchProceso>
          <Observaciones>
           <Obs>
           <Code>15015</Code>
           <Msg> Registra problemas en el domicilio fis-
cal electrónico. No se encuentra adherido </Msg>
           </Obs>
          </Observaciones>
        </ResultGet>
      </FECAEASolicitarResult>
    </FECAEASolicitarResponse>
  </soapenv:Body>
</soap:Envelope>



# Página 93

Método  de consulta de CAEA (FECAEAConsultar)
Este método permite consultar la información correspondiente a un CAEA previamente 
otorgado para un periodo/orden. 
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx  ?op=  FECAEAConsultar  
Mensaje de solicitud
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEAConsultar>
         <ar:Auth>
             <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
         <ar:Periodo>int</ar:Periodo>
         <ar:Orden>short</ar:Orden>
      </ar:FECAEAConsultar>
   </soapenv:Body>
</soapenv:Envelope>
Donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de Token, 
Sign y Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Campo Tipo Detalle Obligatorio
Periodo int (6) Periodo del CAEA.  (yyyymm) S
Orden short (1) Orden del CAEA dentro del periodo. Quincena 1, 
Quincena 2 S



# Página 94

Mensaje de respuesta
Retorna los detalles de los CAEA autorizados para el periodo y orden consultado.
<?xml version=”1.0” encoding=”utf-8”?>
<soap12:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap12=”http://www.w3.org/2003/05/soap-envelope”>
  <soap12:Body>
    <FECAEAConsultarResponse xmlns=”http://ar.gov.afip.dif.FEV1/”>
      <FECAEAConsultarResult>
        <ResultGet>
          <CAEA>string</CAEA>
          <Periodo>int</Periodo>
          <Orden>short </Orden>
          <FchVigDesde>string</FchVigDesde>
          <FchVigHasta>string</FchVigHasta>
          <FchTopeInf>string</FchTopeInf>
          <FchProceso>string</FchProceso>
          <Observaciones>
            <Obs>
               <Code>int</Code>
               <Msg>string</ Msg>
            </Obs>
          </Observaciones>
        </ResultGet>
        <Errors>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
        </Events>
      </FECAEAConsultarResult>
    </FECAEAConsultarResponse>
  </soapenv:Body>
</soapenv:Envelope>
Donde:



# Página 95

FECAEAConsultarResult:
Campo Detalle Obligatorio
ResultGet Información completa de los CAEA Autorizados. S
Errors Información de errores detectados N
Events Información de eventos N
ResultGet: Detalle de un CAEA válido;  esta compuesto por los siguientes campos:
Campo Tipo Detalle Obligatorio
CAEA String (14) Código de Autorización electrónico anticipado N
Periodo Int (6) Periodo (yyyymm) S
Orden Short (1) Orden. Quincena 1, quincena 2 S
FchVigDesde String (8) Fecha de vigencia de CAEA desde N
FchVigHasta String (8) Fecha de vigencia de CAEA hasta N
FchTopeInf String (8) Fecha de tope para informar los 
comprobantes vinculados al  CAEA N
FchProceso String (8) Fecha de proceso N
Validaciones, acciones y errores
Controles aplicados al objeto <FECAEAConsultar>
Validaciones Excluyentes
Campo / 
Grupo
Código de 
error Descripción de la validación
<Periodo> 15004
El valor indicado en el campo <Periodo> es obligatorio.. Debe 
tener formato AAAAMM, donde AAAA indica el año y MM el mes 
en números.
<Orden> 15005 El valor indicado en el campo <Orden> es obligatorio. Valores 
permitidos 1 o 2.
Ejemplo:
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">



# Página 96

   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEAConsultar>
         <ar:Auth>
             <ar:Token>un string</ar:Token>
            <ar:Sign>un string</ar:Sign>
            <ar:Cuit>33000000007</ar:Cuit>
         </ar:Auth>
         <ar:Periodo>201011</ar:Periodo>
         <ar:Orden>1</ar:Orden>
      </ar:FECAEAConsultar>
   </soapenv:Body>
</soapenv:Envelope>
<?xml version=”1.0” encoding=”utf-8”?>
<soap12:Envelope 
xmlns:xsi=”http://www.w3.org/2001/XMLSchema-instance” 
xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap12=”http://www.w3.org/2003/05/soap-envelope”>
  <soap12:Body>
    <FECAEAConsultarResponse 
xmlns=”http://ar.gov.afip.dif.FEV1/”>
      <FECAEAConsultarResult>
        <ResultGet>
          <CAEA>12345678901234</CAEA>
          <Periodo>201011</Periodo>
          <Orden>1</Orden>
          <FchVigDesde>20101101</FchVigDesde>
          <FchVigHasta>20101115</FchVigHasta>
          <FchTopeInf>20101215</FchTopeInf>
          <FchProceso>20101028</FchProceso>
        </ResultGet>
      </FECAEAConsultarResult>
    </FECAEAConsultarResponse>
  </soapenv:Body>
</soap:Envelope>
Recuperador de valores referenciales de códigos de Tipos de comprobante 
(FEParamGetTiposCbte)
Este método permite consultar los tipos de comprobantes habilitados en este WS.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= FEParamGetTiposCbte
Mensaje de solicitud
Recibe las credenciales de autenticación y la cuit del usuario representado.



# Página 97

<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEParamGetTiposCbte>
       <ar:Auth>
        <ar:Token>string</ar:Token>
        <ar:Sign>string</ar:Sign>
        <ar:Cuit>long</ar:Cuit>
      </ar:Auth>
    </ar:FEParamGetTiposCbte>
  </soapenv:Body>
</soapenv:Envelope>
Donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign , Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Mensaje de respuesta
Retorna el  universo  de tipos de comprobante validos.
<?xml version=”1.0” encoding=”utf-8”?>
<soap:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/”>
  <soap:Body>
    <FEParamGetTiposCbteResponse 
xmlns=”http://ar.gov.afip.dif.fev1/”>
      <FEParamGetTiposCbteResult>
        <ResultGet>
          <CbteTipo>
            <Id>int</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </CbteTipo>
          <CbteTipo>
            <Id>int</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </CbteTipo>
        </ResultGet>
        <Errors>
          <Err>
            <Code>int</Code>



# Página 98

            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
        </Events>
      </FEParamGetTiposCbteResult>
    </FEParamGetTiposCbteResponse>
  </soap:Body>
</soap:Envelope>
Donde:
FEParamGetTiposCbteResult:
Campo Detalle Obligatorio
ResultGet Información de los diferentes tipos de comprobantes 
permitidos. N
Errors Información de errores detectados N
Events Información de eventos N
ResultGet: Detalle de los tipos de comprobantes;  esta compuesto por los siguientes campos:
Campo Tipo Detalle Obligatorio
Id Int (3) Código  de comprobante S
Desc String (250) Descripción S
FchDesde String (8) Fecha de vigencia desde S
FchHasta String (8) Fecha de vigencia hasta N
Recuperador de valores referenciales de códigos de Tipos de Conceptos   
(FEParamGetTiposConcepto)
Este método devuelve los tipos de conceptos posibles en este WS.



# Página 99

Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= 
EparamGetTiposConcepto
Mensaje de solicitud
Recibe las credenciales de autenticación y la cuit del usuario representado.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEParamGetTiposConcepto>
           <ar:Auth>
            <ar:Token>sring</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
      </ar:FEParamGetTiposConcepto>
   </soapenv:Body>
</soapenv:Envelope>
donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign , Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Mensaje de respuesta
<?xml version=”1.0” encoding=”utf-8”?>
<soap:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/”>
  <soap:Body>
    <FEParamGetTiposConceptoResponse 
xmlns=”http://ar.gov.afip.dif.fev1/”>
      <FEParamGetTiposConceptoResult>
        <ResultGet>
          <ConceptoTipo>
            <Id>int</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </ConceptoTipo>
          <ConceptoTipo>



# Página 100

            <Id>int</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </ConceptoTipo>
        </ResultGet>
        <Errors>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
        </Events>
      </FEParamGetTiposConceptoResult>
    </FEParamGetTiposConceptoResponse>
  </soap:Body>
</soap:Envelope>
donde:
FEParamGetTiposConceptoResult
Campo Detalle Obligatorio
ResultGet Información de los diferentes tipos de conceptos permitidos. N
Errors Información de errores detectados N
Events Información de eventos N
ResultGet: Detalle de los tipos de conceptos;  esta compuesto por los siguientes campos:
donde:
Campo Tipo Detalle Obligatorio
Id Int (2) Código  de concepto S
Desc String (250) Descripción S
FchDesde String (8) Fecha de vigencia desde S
FchHasta String (8) Fecha de vigencia hasta N



# Página 101

Recuperador de valores referenciales de códigos de Tipos de Documentos  
(FEParamGetTiposDoc)
Este método retorna el universo de tipos de documentos disponibles en el presente WS.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= FEParamGetTiposDoc
Mensaje de solicitud
Recibe las credenciales de autenticación y la cuit del usuario representado.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEParamGetTiposDoc>
         <ar:Auth>
              <ar:Token>string</ar:Token>
              <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
      </ar:FEParamGetTiposDoc>
   </soapenv:Body>
</soapenv:Envelope>
donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign , Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Mensaje de respuesta
<?xml version=”1.0” encoding=”utf-8”?>
<soap:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/”>
  <soap:Body>
    <FEParamGetTiposDocResponse 
xmlns=”http://ar.gov.afip.dif.fev1/”>
      <FEParamGetTiposDocResult>
        <ResultGet>
          <DocTipo>



# Página 102

            <Id>int</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </DocTipo>
          <DocTipo>
            <Id>int</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </DocTipo>
        </ResultGet>
        <Errors>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
        </Events>
      </FEParamGetTiposDocResult>
    </FEParamGetTiposDocResponse>
  </soap:Body>
</soap:Envelope>
donde: 
FEParamGetTiposDocResult:
Campo Detalle Obligatorio
ResultGet Información sobre los tipos de documento aceptados. N
Errors Información de errores detectados N
Events Información de eventos N
ResultGet:
Campo Tipo Detalle Obligatorio
Id Int (2) Código  de tipo  de documento S
Desc String (250) Descripción S
FchDesde String(8) Fecha de vigencia desde S



# Página 103

FchHasta String (8) Fecha de vigencia hasta N
Recuperador de valores referenciales de códigos de Tipos de Alícuotas 
(FEParamGetTiposIva)
Mediante este método se obtiene la totalidad de alícuotas de IVA posibles de uso en el 
presente WS, detallando código y descripción.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= FEParamGetTiposIva
Mensaje de solicitud
Recibe las credenciales de autenticación y la cuit del usuario representado.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEParamGetTiposIva>
           <ar:Auth>
            <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
      </ar:FEParamGetTiposIva>
   </soapenv:Body>
</soapenv:Envelope>
donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign , Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Mensaje de respuesta
<?xml version=”1.0” encoding=”utf-8”?>
<soap:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/”>



# Página 104

  <soap:Body>
    <FEParamGetTiposIvaResponse 
xmlns=”http://ar.gov.afip.dif.fev1/”>
      <FEParamGetTiposIvaResult>
        <ResultGet>
          <IvaTipo>
            <Id>string</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </IvaTipo>
          <IvaTipo>
            <Id>string</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </IvaTipo>
        </ResultGet>
        <Errors>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
        </Events>
      </FEParamGetTiposIvaResult>
    </FEParamGetTiposIvaResponse>
  </soap:Body>
</soap:Envelope>
donde: 
FEParamGetTiposIvaResult:
Campo Detalle Obligatorio
ResultGet Información sobre los tipos de alícuotas permitidas. 
Contiene los datos IvaTipo N
Errors Información de errores detectados N
Events Información de eventos N
IvaTipo



# Página 105

Campo Tipo Detalle Obligatorio
Id Int (2) Tipo  de IVA S
Desc String (250) Descripción S
FchDesde String (8) Fecha de vigencia desde S
FchHasta String (8) Fecha de vigencia hasta N
Recuperador de valores referenciales de códigos de Tipos de Monedas 
(FEParamGetTiposMonedas)
Este método retorna el universo de Monedas disponibles en el presente WS, indicando id y 
descripción de cada una.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= 
FEParamGetTiposMonedas
Mensaje de solicitud
Recibe las credenciales de autenticación y la cuit del usuario representado.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEParamGetTiposMonedas>
           <ar:Auth>
              <ar:Token>string</ar:Token>
              <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
      </ar:FEParamGetTiposMonedas>
   </soapenv:Body>
</soapenv:Envelope>
donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign , Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S



# Página 106

Campo Detalle Obligatorio
Mensaje de respuesta
<?xml version=”1.0” encoding=”utf-8”?>
<soap:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/”>
  <soap:Body>
    <FEParamGetTiposMonedasResponse 
xmlns=”http://ar.gov.afip.dif.fev1/”>
      <FEParamGetTiposMonedasResult>
        <ResultGet>
          <Moneda>
            <Id>string</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </Moneda>
          <Moneda>
            <Id>string</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </Moneda>
        </ResultGet>
        <Errors>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
        </Events>
      </FEParamGetTiposMonedasResult>
    </FEParamGetTiposMonedasResponse>
  </soap:Body>
</soap:Envelope>
donde:
FEParamGetTiposMonedasResult:



# Página 107

Campo Detalle Obligatorio
ResultGet Información de los tipos de monedas disponibles. N
Errors Información de errores detectados N
Events Información de eventos N
ResultGet: Detalle de los tipos de monedas;  esta compuesto por los siguientes cam -
pos:
Campo Tipo Detalle Obligatorio
Id String (3) Código  de moneda S
Desc String (250) Descripción S
FchDesde String (8) Fecha de vigencia desde S
FchHasta String (8) Fecha de vigencia hasta N
Recuperador de valores referenciales de códigos de Tipos de datos Opcionales 
(FEParamGetTiposOpcional)
Este método permite consultar los códigos y descripciones de los tipos de datos Opcionales 
que se encuentran habilitados para ser usados en el WS.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= FEParamGetTiposOpcional
Mensaje de solicitud
Recibe las credenciales de autenticación y la cuit del usuario representado.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEParamGetTiposOpcional>
          <ar:Auth>
            <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
      </ar:FEParamGetTiposOpcional>
   </soapenv:Body>
</soapenv:Envelope>



# Página 108

donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos 
de Token, Sign , Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Mensaje de respuesta
<?xml version=”1.0” encoding=”utf-8”?>
<soap:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/”>
  <soap:Body>
    <FEParamGetTiposOpcionalResponse 
xmlns=”http://ar.gov.afip.dif.fev1/”>
      <FEParamGetTiposOpcionalResult>
        <ResultGet>
          <OpcionalTipo>
            <Id>string</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </OpcionalTipo>
          <OpcionalTipo>
            <Id>string</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </OpcionalTipo>
        </ResultGet>
        <Errors>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>



# Página 109

          </Evt>
        </Events>
      </FEParamGetTiposOpcionalResult>
    </FEParamGetTiposOpcionalResponse>
  </soap:Body>
</soap:Envelope>
donde:
FEParamGetTiposOpcionalResult:
Campo Detalle Obligatorio
ResultGet Información de los tipos de datos opcionales N
Errors Información de errores detectados N
Events Información de eventos N
ResultGet: Detalle de los tipos de datos opcionales; esta compuesto por los siguientes campos:
Campo Tipo Detalle Obligatorio
Id String (4) Identificador de campo  opcional S
Desc String (250) Descripción S
FchDesde String (8) Fecha de vigencia desde S
FchHasta String (8) Fecha de vigencia hasta N
Recuperador de valores referenciales de códigos de Tipos de Tributos 
(FEParamGetTiposTributos)
Devuelve los posibles códigos de tributos que puede contener un comprobante y su 
descripción.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op=FEParamGetTiposTributos
Mensaje de solicitud
Recibe las credenciales de autenticación y la cuit del usuario representado.



# Página 110

<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEParamGetTiposTributos>
          <ar:Auth>
            <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
      </ar:FEParamGetTiposTributos>
   </soapenv:Body>
</soapenv:Envelope>
donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign , Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Mensaje de respuesta
<?xml version=”1.0” encoding=”utf-8”?>
<soap:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/”>
  <soap:Body>
    <FEParamGetTiposTributosResponse 
xmlns=”http://ar.gov.afip.dif.fev1/”>
      <FEParamGetTiposTributosResult>
        <ResultGet>
          <TributoTipo>
            <Id>short</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </TributoTipo>
          <TributoTipo>
            <Id>short</Id>
            <Desc>string</Desc>
            <FchDesde>string</FchDesde>
            <FchHasta>string</FchHasta>
          </TributoTipo>
        </ResultGet>
        <Errors>
          <Err>



# Página 111

            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
        </Events>
      </FEParamGetTiposTributosResult>
    </FEParamGetTiposTributosResponse>
  </soap:Body>
</soap:Envelope>
donde:
FEParamGetTiposTributosResult:
Campo Detalle Obligatorio
ResultGet Información de los tipos de tributos posibles. N
Errors Información de errores detectados N
Events Información de eventos N
ResultGet: Detalle de los tipos de tributos;  esta compuesto por los siguientes campos:
Campo Tipo Detalle Obligatorio
Id Int (2) Código  de Tributo S
Desc String (250) Descripción S
FchDesde String (8) Fecha de vigencia desde S
FchHasta String (8) Fecha de vigencia hasta N
Recuperador de los puntos de venta asignados a Facturación Electrónica que soporten 
CAE y CAEA vía Web Services (FEParamGetPtosVenta)
Este método permite consultar los puntos de venta para ambos tipos de Código de 
Autorización (CAE y CAEA) gestionados previamente por la CUIT emisora.



# Página 112

Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= FEParamGetPtosVenta
Mensaje de solicitud
Recibe las credenciales de autenticación, cuit del usuario representado.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEParamGetPtosVenta>
        <ar:Auth>
           <ar:Token>string</ar:Token>
           <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
      </ar:FEParamGetPtosVenta>
   </soapenv:Body>
</soapenv:Envelope>
donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign , Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Mensaje de respuesta
<?xml version=”1.0” encoding=”utf-8”?>
<soap:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/”>
  <soap:Body>
    <FEParamGetPtosVentaResponse 
xmlns=”http://ar.gov.afip.dif.FEV1/”>
      <FEParamGetPtosVentaResult>
        <ResultGet>
          <PtoVenta>
            <Nro>int</Nro>
            <EmisionTipo>string</EmisionTipo>



# Página 113

            <Bloqueado>string</Bloqueado>
            <FchBaja>string</FchBaja>
          </PtoVenta>
          <PtoVenta>
            <Nro>int</Nro>
            <EmisionTipo>string</EmisionTipo>
            <Bloqueado>string</Bloqueado>
            <FchBaja>string</FchBaja>
          </PtoVenta>
        </ResultGet>
        <Errors>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
        </Events>
      </FEParamGetPtosVentaResult>
    </FEParamGetPtosVentaResponse>
  </soap:Body>
</soap:Envelope>
donde:
 FEParamGetPtosVentaResult:
Campo Detalle Obligatorio
ResultGet Información de los puntos de venta electrónicos habilitados 
para CAE o CAEA.  Contiene el dato PtoVenta N
PtoVenta Información sobre los puntos de venta S
Errors Información de errores detectados N
Events Información de eventos N
PtoVenta: Detalle de los tipos puntos de venta electrónicos;  esta compuesto por los si-
guientes campos:



# Página 114

Campo Tipo Detalle Obligatorio
Nro Int (5) Punto de venta S
EmisionTipo String (8) Identifica si es punto de venta para CAE o 
CAEA S
Bloqueado String (1)
Indica si el  punto  de venta esta bloqueado. 
De darse esta situación se deberá ingresar al 
ABM de puntos de venta a regularizar la 
situación Valores S o N
S
FchBaja String (8) Indica la fecha de baja en caso  de estarlo N
Recuperador de cotización de moneda (FEParamGetCotizacion)
Retorna la última cotización de la base de datos aduanera de la moneda ingresada. Este valor 
es orientativo.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= 
FEParamGetCotizacion
Mensaje de solicitud
Recibe las credenciales de autenticación, cuit del usuario representado y  el código  de 
moneda.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEParamGetCotizacion>
          <ar:Auth>
           <ar:Token>string</ar:Token>
           <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
        <ar:MonId>string</ar:MonId>
        <!--Optional:-->
        <ar:FchCotiz>string</ar:FchCotiz>
      </ar:FEParamGetCotizacion>
   </soapenv:Body>
</soapenv:Envelope>
donde:



# Página 115

Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign , Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Campo Detalle Obligatorio
MonId Código  de moneda de la que se solicita cotización. S
FchCotiz Fecha a la cual se consulta la cotización de la moneda 
ingresada N
Mensaje de respuesta
<?xml version=”1.0” encoding=”utf-8”?>
<soap:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/”>
  <soap:Body>
    <FEParamGetCotizacionResponse 
xmlns=”http://ar.gov.afip.dif.FEV1/”>
      <FEParamGetCotizacionResult>
        <ResultGet>
          <MonId>string</MonId>
          <MonCotiz>double</MonCotiz>
          <FchCotiz>string</FchCotiz>
        </ResultGet>
        <Errors>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>



# Página 116

        </Events>
      </FEParamGetCotizacionResult>
    </FEParamGetCotizacionResponse>
  </soap:Body>
</soap:Envelope>
donde:
FEParamGetCotizacionResult:
Campo Detalle Obligatorio
ResultGet cotización de la moneda solicitada y fecha de la misma S
Errors Información de errores detectados N
Events Información de eventos N
ResultGet:
Campo Tipo Detalle Obligatorio
MonCotiz Double (4+6) Cotización de la moneda N
MonId String (3) Código  de moneda S
FchCotiz String (8) Fecha de la cotización. 
Formato yyyymmdd N
Validaciones y errores 
Validaciones que se aplican sobre el objeto <FEParamGetCotizacion>
Validaciones Excluyentes



# Página 117

Campo/Grupo Código 
de error Descripción de la validación
MonId 12000
Campo < MonId> debe ser algunos de los habilitados en el presente  
WS.  Para consultar los valores posible utilizar el método FEParamGe -
tTiposMonedas
MonId 12001 Campo <MonId> es obligatorio ingresarlo.
FchCotiz 12002 Campo FchCotiz No corresponde a una fecha valida con formato YYY -
YMMDD
Método  para informar CAEA sin movimiento (FECAEASinMovimientoInformar)
Esta operación permite informar a la administración cuales fueron los CAEA’s otorgados que 
no sufrieron movimiento alguno para un determinado punto de venta. El cliente envía el 
requerimiento, el cual es atendido por el WS, superadas las validaciones de seguridad se 
registrara la fecha por la cual se informo la falta de movimientos.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= 
FECAEASinMovimientoInformar
Mensaje de solicitud
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEASinMovimientoInformar>
        <ar:Auth>
          <ar:Token>string</ar:Token>
          <ar:Sign>string</ar:Sign>
           <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
         <ar:PtoVta>int</ar:PtoVta>
        <ar:CAEA>string</ar:CAEA>
      </ar:FECAEASinMovimientoInformar>
   </soapenv:Body>
</soapenv:Envelope>
donde:



# Página 118

Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign y Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Campo Detalle Obligatorio
PtoVta Punto de Venta para el que no se utilizó el CAEA informado S
CAEA CAEA que se está informando como no utilizado para el 
punto de venta indicado S
Mensaje de respuesta
Retorna el resultado del proceso de informar un CAEA como no utilizado.
<?xml version=”1.0” encoding=”utf-8”?>
<soap12:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap12=”http://www.w3.org/2003/05/soap-envelope”>
  <soap12:Body>
    <FECAEASinMovimientoResponse 
xmlns=”http://ar.gov.afip.dif.FEV1/”>
      <FECAEASinMovimientoResult>
        <CAEA>string</CAEA>
        <FchProceso>string</FchProceso>
        <Resultado>string</Resultado>
        <PtoVta>int</PtoVta>
        <Errors>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
        </Events>
      </FECAEASinMovimientoResult>



# Página 119

    </FECAEASinMovimientoResponse>
  </soapenv:Body>
</soapenv:Envelope>
donde:
Campo Detalle Obligatorio
FECAEASinMovimi
entoResult
Información completa del  CAEA sin movimientos. Contiene 
los datos CAEA, FchProceso, Resultado, PtoVta, Errors y 
Events.
S
Errors Información de errores detectados N
Events Información de eventos N
FECAEASinMovimientoResult: El objeto resultante informante del resultado del proceso 
contiene los siguientes campos:
Campo Tipo Detalle Obligatorio
CAEA String (14) Código de Autorización electrónico 
anticipado S
FchProceso String (8) Fecha de Procesamiento del CAEA 
informado como sin movimientos N
Resultado String (1) Aprobado o Rechazado N
PtoVta Int (5) Punto de venta vinculado al CAEA 
informado. S
Validaciones y errores 
Controles aplicados:
Campo / 
Grupo
Código de 
Error Validación
<CAEA> 1200 El código de CAEA que se está informando debe ser del tipo de código 
de autorización CAEA
<CUIT> 1201 Corresponda a la CUIT del Emisor indicada en <Auth><Cuit> 
<CAEA> / 1202 Que el CAEA / PtoVta no esté informado como utilizado en algún 



# Página 120

<PtoVta> comprobante
Fecha de 
envío de la 
solicitud
1203 La fecha de envío de la solicitud debe ser mayor a la fecha de inicio de 
vigencia del CAEA que se está informando.
<PtoVta> 1204 El PtoVta debe corresponder a un punto de venta habilitado para el 
régimen CAEA
<PtoVta> 1205 El punto de venta deberá haber estado activo durante la vigencia del 
CAEA
<PtoVta> 1206 El punto de venta deberá haber estar comprendido  entre 1 y 99998
<CAEA> 1207 CAEA y formato válido
PtoVta 1209 El punto de venta informado como sin movimiento ya fue notificado
Método Dummy para verificación de funcionamiento de infraestructura (FEDummy)
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= FEDummy
Mensaje de solicitud
El método no posee parámetros de ingreso
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEDummy/>
   </soapenv:Body>
</soapenv:Envelope>
Mensaje de respuesta
Retorna  la comprobación vía “ping” de los elementos principales de infraestructura del  
servicio.
<?xml version=”1.0” encoding=”utf-8”?>
<soap:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/”>
  <soap:Body>
    <FEDummyResponse xmlns=”http://ar.gov.afip.dif.FEV1/”>
      <FEDummyResult>
        <AppServer>string</AppServer>
        <DbServer>string</DbServer>



# Página 121

        <AuthServer>string</AuthServer>
      </FEDummyResult>
    </FEDummyResponse>
  </soap:Body>
</soap:Envelope>
donde:
Campo Tipo Detalle Obligatorio
AppServer String (2) Servidor de aplicaciones S
DbServer String (2) Servidor de base de datos S
AuthServer String (2) Servidor de autenticación S
Recuperador de ultimo valor de comprobante registrado (FECompUltimoAutorizado)
Retorna el ultimo comprobante autorizado para el tipo de comprobante / cuit / punto de venta 
ingresado / Tipo de Emisión
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?
op=FECompUltimoAutorizado 
Mensaje de solicitud
Recibe las credenciales de autenticación y la cuit del usuario representado.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECompUltimoAutorizado>
         <ar:Auth>
          <ar:Token>string</ar:Token>
          <ar:Sign>string</ar:Sign>
          <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
         <ar:PtoVta>int</ar:PtoVta>
         <ar:CbteTipo>int</ar:CbteTipo>
      </ar:FECompUltimoAutorizado>
   </soapenv:Body>
</soapenv:Envelope>



# Página 122

donde:
FECompUltimoAutorizado:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign y Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Campo Detalle Obligatorio
PtoVta Punto  de venta S
CbteTipo Tipo de comprobante S
Mensaje de respuesta
Retorna el  último número de comprobante registrado para el punto de venta y tipo de 
comprobante enviado.
<?xml version=”1.0” encoding=”utf-8”?>
<soap:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/”>
  <soap:Body>
    <FECompUltimoAutorizadoResponse 
xmlns=”http://ar.gov.afip.dif.FEV1/”>
      <FECompUltimoAutorizadoResult>
        <PtoVta>int</PtoVta>
        <CbteTipo>int</CbteTipo>
        <CbteNro>int</CbteNro>
        <Errors>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>



# Página 123

          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
        </Events>
      </FECompUltimoAutorizadoResult>
    </FECompUltimoAutorizadoResponse>
  </soap:Body>
</soap:Envelope>
donde:
FECompUltimoAutorizadoResult:
Campo Detalle Obligatorio
FECompUltimoAut
orizadoResul
Información completa del  CAEA sin movimientos. Contiene 
PtoVta, CbteTipo, CbteNro, Errors y Events S
Errors Información de errores detectados N
Events Información de eventos N
Campo Tipo Detalle Obligatorio
PtoVta Int (5) Punto  de venta S
CbteTipo Int (3) Tipo de comprobante S
CbteNro Long (8) Número de comprobante N
Validaciones, acciones y errores
Controles aplicados:
Campo / 
Grupo
Código de 
Error Validación
<PtoVta> 11000 El PtoVta debe ser válido comprendido entre 1 y 99998
<CbteTipo> 11001 Debe de ser algunos de los habilitados en este WS. Consultar método 
FEParamGetTiposCbte
<PtoVta> 11002 Debe ser un punto de venta habilitado en este WS. Consultar método 
FEParamGetPtosVenta



# Página 124

Recuperador de cantidad máxima de registros FECAESolicitar / FECAEARegInformativo  
(FECompTotXRequest)
Retorna la cantidad máxima de registros que se podrá incluir en un request al  método 
FECAESolicitar / FECAEARegInformativo.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?op= FECompTotXRequest 
Mensaje de solicitud
Recibe las credenciales de autenticación y la cuit del usuario representado.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECompTotXRequest>
           <ar:Auth> 
            <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
      </ar:FECompTotXRequest>
   </soapenv:Body>
</soapenv:Envelope>
donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign y Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Mensaje de respuesta
<?xml version=”1.0” encoding=”utf-8”?>



# Página 125

<soap:Envelope xmlns:xsi=”http://www.w3.org/2001/XMLSchema-
instance” xmlns:xsd=”http://www.w3.org/2001/XMLSchema” 
xmlns:soap=”http://schemas.xmlsoap.org/soap/envelope/”>
  <soap:Body>
   <FECompTotXRequestResponse xmlns=”http://ar.gov.afip.dif.FEV1/”>
      <FECompTotXRequestResult>
        <RegXReq>int</RegXReq>
        <Errors>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
          <Err>
            <Code>int</Code>
            <Msg>string</Msg>
          </Err>
        </Errors>
        <Events>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
          <Evt>
            <Code>int</Code>
            <Msg>string</Msg>
          </Evt>
        </Events>
      </FECompTotXRequestResult>
    </FECompTotXRequestResponse>
  </soap:Body>
</soap:Envelope>
Dónde:
FECompTotXRequestResult:
Campo Detalle Obligatorio
FECompTotXRequ
estResult Contiene los datos RegXReq, Errors y Events. S
Errors Información de errores detectados N
Events Información de eventos N
Campo Tipo Detalle Obligatorio
RegXReq Int (4)
Cantidad máxima de registros que se pueden 
incluir en un Request de solicitud de CAE e 
Informar CAEA.
S



# Página 126

Método para informar comprobantes emitidos con  CAEA (FECAEARegInformativo)
Este método permite informar para cada CAEA otorgado, la totalidad de los comprobantes 
emitidos y asociados a cada CAEA. Por cada comprobante se enviará una solicitud, la cual será 
procesada por el WS pudiendo producirse alguna de las siguientes situaciones:
 Supere todas las validaciones, la solicitud es aprobada. 
 No supere alguna de las validaciones excluyentes, la solicitud será rechazada.
 No supere alguna de las validaciones no excluyentes, la solicitud es aprobada con 
observaciones.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx?
op=FECAEARegInformativo
Mensaje de solicitud
Recibe la información del comprobante o lote de comprobantes.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEARegInformativo>
         <ar:Auth>
            <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
         <ar:FeCAEARegInfReq>
            <ar:FeCabReq>
               <ar:CantReg>int</ar:CantReg>
               <ar:PtoVta>int</ar:PtoVta>
               <ar:CbteTipo>int</ar:CbteTipo> 
            </ar:FeCabReq>
            <ar:FeDetReq>
               <ar:FECAEADetRequest> 
                  <ar:Concepto>int</ar:Concepto>
                  <ar:DocTipo>int</ar:DocTipo>
                  <ar:DocNro>long</ar:DocNro>
                  <ar:CbteDesde>long</ar:CbteDesde>
                  <ar:CbteHasta>long</ar:CbteHasta>
                  <ar:CbteFch>string</ar:CbteFch>
                  <ar:ImpTotal>double</ar:ImpTotal>
                  <ar:ImpTotConc>double</ar:ImpTotConc>
                  <ar:ImpNeto>double</ar:ImpNeto>
                  <ar:ImpOpEx>double</ar:ImpOpEx>
                  <ar:ImpIVA>double</ar:ImpIVA>



# Página 127

                  <ar:ImpTrib>double</ar:ImpTrib>
                  <ar:FchServDesde>string</ar:FchServDesde>
                  <ar:FchServHasta>string</ar:FchServHasta>
                  <ar:FchVtoPago>string</ar:FchVtoPago>
                  <ar:MonId>string</ar:MonId>
                  <ar:MonCotiz>double</ar:MonCotiz>
                  <ar:CanMisMonExt>string</ar:CanMisMonExt>
                  <ar:CondicionIVAReceptorId>int</ar:CondicionIVA-
ReceptorId>
                  <ar:CbtesAsoc>
                     <ar:CbteAsoc>
                        <ar:Tipo>short</ar:Tipo>
                        <ar:PtoVta>int</ar:PtoVta>
                        <ar:Nro>long</ar:Nro>
                        <ar:Cuit>String</ar:Cuit>
                        <ar:CbteFch>String</ar:CbteFch>
                     </ar:CbteAsoc>
                  </ar:CbtesAsoc>
                  <ar:Tributos>
                     <ar:Tributo>
                        <ar:Id>short</ar:Id>
                        <ar:Desc>string</ar:Desc>
                        <ar:BaseImp>double</ar:BaseImp>
                        <ar:Alic>double</ar:Alic>
                        <ar:Importe>double</ar:Importe>
                     </ar:Tributo>
                  </ar:Tributos>
                  <ar:Iva>
                     <ar:AlicIva>
                        <ar:Id>short</ar:Id>
                        <ar:BaseImp>double</ar:BaseImp>
                        <ar:Importe>double</ar:Importe>
                     </ar:AlicIva>
                  </ar:Iva>
                  <ar:Opcionales>
                     <ar:Opcional>
                        <ar:Id>string</ar:Id>
                        <ar:Valor>string</ar:Valor>
                     </ar:Opcional>
                  </ar:Opcionales>
                  <ar:PeriodoAsoc>
                     <ar:FchDesde>string</ar:FchDesde>
                     <ar:FchHasta>string</ar:FchHasta>
                  </ar:PeriodoAsoc>
                  <ar:CAEA>string</ar:CAEA>
                  <ar:CbteFchHsGen>string</ar:CbteFchHsGen>
                  <ar:Actividades>
                     <ar:Actividad>
                        <ar:Id>Long</ar:Id>
                     </ar:Actividad>
                  </ar:Actividades>
              </ar:FECAEADetRequest>
            </ar:FeDetReq>
         </ar:FeCAEARegInfReq>
      </ar:FECAEARegInformativo>
   </soapenv:Body>
</soapenv:Envelope>



# Página 128

Dónde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign y Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Campo Detalle Obligatorio
FeCAEARegInfReq Información del comprobante o lote de comprobantes de 
ingreso. Contiene los datos de FeCabReq y FeDetReq S
FeCabReq Información de la cabecera del comprobante o lote de 
comprobantes de ingreso S
FeDetReq / 
FECAEADetRequest
Información del detalle del comprobante o lote de 
comprobantes de ingreso. S
FeCabReq: La cabecera del comprobante o lote de comprobantes de ingreso está compuesta 
por los siguientes campos:
Campo Tipo Detalle Obligatorio
CantReg Int (4) Cantidad de registros del detalle del comprobante o 
lote de comprobantes de ingreso S
CbteTipo Int (3)
Tipo de comprobante que se está informando.  Si se 
informa más de un comprobante, todos deben ser del 
mismo tipo.
S
PtoVta Int (5)
Punto de Venta del comprobante que se está 
informando. Si se informa más de un comprobante, 
todos deben corresponder al mismo punto de venta.
S
FeDetReq: El detalle del comprobante o lote de comprobantes de ingreso esta compuesto por 
los siguientes campos: 
Campo Tipo Detalle Obligatorio
Concepto Int (2)
Concepto del comprobante. Valores 
permitidos 
1 Productos
2 Servicios
3 Productos y Servicios
S
DocTipo Int (2) Código de documento identificatorio del S



# Página 129

Campo Tipo Detalle Obligatorio
comprador
DocNro Long (11) Nro.  De identificación del comprador S
CbteDesde Long (8)
Nro. De comprobante desde 
Rango 1- 99999999
S
CbteHasta Long (8)
Nro. De comprobante registrado hasta
Rango 1- 99999999
S
CbteFch String (8)
Fecha del comprobante (yyyymmdd). Para 
Concepto igual a 1, la fecha de emisión del 
comprobante puede ser hasta más 5 días 
respecto de la fecha de generación. La misma 
no podrá exceder el mes de presentación. Si se 
indica Concepto igual a 2 ó 3 puede ser hasta 
10 días anteriores o posteriores a la fecha de 
generación 
N
ImpTotal Double 
(13+2)
Importe  total  del  comprobante, Debe ser 
igual a Importe neto no gravado + Importe 
exento + Importe neto gravado + todos los 
campos de IVA  al XX% + Importe de tributos
S
ImpTotConc Double 
(13+2)
Importe neto no gravado. 
Debe ser menor o igual a Importe total y no 
puede ser menor a cero. 
S
ImpNeto Double 
(13+2)
Importe neto  gravado. Debe ser menor o igual 
a Importe total y no puede ser menor a cero. S
ImpOpEx Double 
(13+2)
Importe exento. Debe ser menor o igual a 
Importe total y no puede ser menor a cero. S
ImpIVA Double 
(13+2) Suma de los importes del  array de IVA S
ImpTrib Double 
(13+2) Suma de los importes del  array de tributos S
FchServDesde String (8)
Fecha de inicio  del abono para el  servicio a 
facturar. Dato  obligatorio  para concepto  2 o 
3 (Servicios / Productos y Servicios). Formato  
yyyymmdd
N
FchServHasta String (8)
Fecha de fin del abono para el  servicio a 
facturar. Dato  obligatorio  para concepto  2 o 
3 (Servicios / Productos y Servicios). Formato  
yyyymmdd. FchServHasta no puede ser menor 
a FchServDesde
N
FchVtoPago String (8) Fecha de vencimiento  del  pago servicio a 
facturar. Dato  obligatorio  para concepto  2 o 
N



# Página 130

Campo Tipo Detalle Obligatorio
3 (Servicios / Productos y Servicios). Formato  
yyyymmdd. Debe ser igual o posterior a la 
fecha del comprobante.
MonId String (3)
Código  de moneda del  comprobante. 
Consultar método  FEParamGetMonedas para 
valores posibles
S
MonCotiz
Double
(4+6)
Cotización de la moneda informada. Para PES, 
pesos  argentinos  la misma debe ser 1. De 
informar el campo, el mismo no puede quedar 
vacío.
N
CanMisMonExt String (1)
Marca que identifica si el comprobante se 
cancela en misma moneda del comprobante 
(moneda extranjera). Valores posibles S o N.
N
CondicionIVARec
eptorId Int (2)
Condición Frente al IVA del receptor. Consultar 
método “FEParamGetCondicionIvaReceptor”
Campo obligatorio. Si el valor informado no es 
valido, para CAE rechazará y en CAEA 
observará. Si el valor no existe rechazará en 
ambos casos.
N
CbtesAsoc Array Array para informar los comprobantes 
asociados <CbteAsoc> N
Tributos Array Array para informar los tributos asociados a un 
comprobante <Tributo>. N
IVA Array Array para informar las alícuotas y sus 
importes asociados a un comprobante. N
Opcionales Array Array  de campos  auxiliares. Reservado  usos 
futuros. Adicionales por R.G. N
PeriodoAsoc Periodo
Estructura compuesta por la fecha desde y la 
fecha hasta del periodo que se quiere 
identificar
N
CAEA String (14) Código de Autorización electrónico anticipado S
CbteFchHsGen String(14) Fecha y Hora de generación del comprobante 
por contingencia. Formato yyyymmddhhmiss N
Actividades Actividad Array para informar las actividades asociadas a 
un comprobante. N
CbteAsoc: Detalle de los comprobantes relacionados con el comprobante que se está 
informando (array).
Campo Tipo Detalle Obligatorio
Tipo Int (3) Código  de tipo de comprobante. Consultar S



# Página 131

Campo Tipo Detalle Obligatorio
método  FEParamGetTiposCbte
PtoVta Int (5) Punto  de venta S
Nro Long (8) Numero de comprobante S
Cuit Long (11) Cuit Emisor del comprobante N
CbteFch String(8) Fecha del comprobante asociado . Formato  
yyyymmdd N
Tributos: Detalle de tributos relacionados con el  comprobante que se está informando 
(array).
Campo Tipo Detalle Obligatorio
Id Int Código  tributo según método   
FEParamGetTiposTributos S
Desc String (80) Descripción del  tributo. N
BaseImp Double (13+2) Base imponible para la determinación del  
tributo. S
Alic Double (3+2) Alícuota S
Importe Double (13+2) Importe del  tributo S
IVA: Detalle de alícuotas relacionadas con el comprobante que se está informando  
(array).
Campo Tipo Detalle Obligatorio
Id Int (2) Código  de tipo de iva. Consultar método  
FEParamGetTiposIva
S
BaseImp Double (13+2) Base imponible para la determinación de la 
alícuota.
S
Importe Double (13+2) Importe S
Opcionales: Campos auxiliares (array). Adicionales por R.G.
Los datos opcionales sólo deberán ser incluidos si el emisor pertenece al conjunto de emisores 
habilitados a informar opcionales. En ese caso podrá incluir el o los datos opcionales que 
correspondan, especificando el identificador de dato opcional de acuerdo a la situación del 
emisor. El listado de tipos de datos opcionales se puede consultar con el método 
FEParamGetTiposOpcional. 
Ejemplo: si el emisor está incluido en el “Régimen de Promoción Industrial”, deberá incluir un 
array de opcionales con un registro como el sig 



# Página 132

<ar:Opcionales>
<ar:Opcional>
<ar:Id>2</ar:Id>
<ar:Valor>12345678</ar:Valor>
</ar:Opcional>
</ar:Opcionales>
Campo Tipo Detalle Obligatorio
Id String(4) Código  de Opcional, consultar método 
FEParamGetTiposOpcional S
Valor String (250) Valor S
Periodo: Estructura que permite soportar un rango de fechas.
Campo Tipo Detalle Obligatorio
FchDesde String(8)
Fecha correspondiente al inicio del perio-
do de los comprobantes que se quiere 
identiricar
S
FchHasta String(8)
Fecha correspondiente al fin del periodo  
de los comprobantes que se quiere iden -
tificar
S
Actividad: Detalle de la actividad relacionada con las actividades (array) que se indi -
can en el comprobante a autorizar.
Campo Tipo Detalle Obligatorio
Id Long (6) Código  actividad según método   
FEParamGetActividades S
Mensaje de respuesta
Retorna la información del comprobante o lote de comprobantes de ingreso. Ante cualquier 
anomalía se retorna un array errores detectados (Errors) o un array de observaciones según 
corresponda. 
<soap:Envelope xmlns:soap=”http://www.w3.org/2003/05/soap-envelope” 
xmlns:ar=”http://ar.gov.afip.dif.fev1/”>
   <soap:Header/>



# Página 133

   <soap:Body>
      <FECAEARegInformativoResponse>
         <FECAEARegInformativoResult>
            <FeCabResp>
               <Cuit>long</Cuit>
               <PtoVta>int</PtoVta>
               <CbteTipo>int</CbteTipo>
               <FchProceso>string</FchProceso>
               <CantReg>int</CantReg>
               <Resultado>string</Resultado>
            </FeCabResp>
            <FeDetResp>
               <FECAEADetResponse>
                  <Concepto>int</Concepto>
                  <DocTipo>int</DocTipo>
                  <DocNro>long</DocNro>
                  <CbteDesde>long</CbteDesde>
                  <CbteHasta>long</CbteHasta>
                  <Resultado>string</Resultado>
                  <CAEA>string</CAEA>
                  <CbteFch>string</CbteFch>
                  <Obs>
                     <Observaciones>
                        <Code>int</Code>
                        <Msg>string</Msg>
                     </Observaciones>
                  </Obs>
               </FECAEADetResponse>
            </FeDetResp>
            <Events>
               <Evt>
                  <Code>int</Code>
                  <Msg>string</Msg>
               </Evt>
            </Events>
            <Errors>
               <Err>
                  <Code>int</Code>
                  <Msg>string</Msg>
               </Err>
            </Errors>
         </FECAEARegInformativoResult>
      </FECAEARegInformativoResponse>
   </soap:Body>
</soap:Envelope>
Dónde:
Campo Detalle Obligatorio
FECAEARegInformativo
Result
Información del comprobante o lote de comprobantes de 
ingreso, S
FeCabResp
Información de la cabecera del comprobante o lote de 
comprobantes enviada en el request + atributos 
adicionales como resultado y fecha de proceso. 
S
FeDetResp / Información del detalle del comprobante o lote de S



# Página 134

FECAEADetResponse
comprobantes de ingreso + atributos adicionales como 
ser:
resultado del procesamiento.
Fecha del comprobante. 
Observaciones sobre el comprobante.
Errors Información de errores detectados N
Events Información de eventos N
FeCabResp: La cabecera del comprobante o lote de comprobantes de ingreso estará 
compuesta por los siguientes campos:
Campo Tipo Detalle Obligatorio
Cuit Long (11) Cuit del contribuyente S
PtoVta Int (5) Punto  de venta S
CbteTipo Int (3) Tipo  de comprobante S
FchProceso String (14) Fecha de proceso  formato  yyyymmddhhmiss S
CantReg Int (4) Cantidad de registros del detalle del comprobante 
o lote de comprobantes de ingreso S
Resultado String (1) Resultado S
FeDetResp: El detalle del comprobante o lote de comprobantes de ingreso estará compuesto 
por los siguientes campos: 
Campo Tipo Detalle Obligatorio
Concepto Int (2) Concepto S
DocTipo Int (2) Código de documento identificatorio del 
comprador S
DocNro Long (11) Nro.  De identificación del comprador S
CbteDesde Long (8) Nro. De comprobante desde S
CbteHasta Long (8) Nro. De comprobante registrado hasta S
CbteFch String (8) Fecha del comprobante N
Resultado String (1) Resultado S
CAEA String (14) Código de Autorización electrónico anticipado N



# Página 135

Campo Tipo Detalle Obligatorio
Observaciones Array Detalle de observaciones, del comprobante N
Observaciones: La estructura de datos Obs muestra el  detalle de observaciones para un 
comprobante determinado;   estará compuesta por los siguientes campos: 
Campo Tipo Detalle Obligatorio
Code Int (5) Código  de observación S
Msg String (255) Mensaje S
Validaciones y errores 
Controles aplicados al objeto < Auth>
Validaciones Excluyentes
Campo / 
Grupo
Código de 
error Descripción de la validación
<Auth><Cuit> 10000 La CUIT del emisor debe estar registrada y activa en las bases de la 
Administración.
Controles aplicados al objeto <FeCabReq> 
Validaciones Excluyentes
Campo / 
Grupo
Código de 
error Descripción de la validación
<CantReg> 10001 Cantidad de registros de detalle del comprobante o lote de comprobantes 
de ingreso <CantReg> debe estar comprendido entre 1 y 9998
<CantReg> 10002
La cantidad de registros del detalle del comprobante o lote de 
comprobantes de ingreso debe ser igual a lo informado en cabecera del 
comprobante o lote de comprobantes de ingreso <CantReg>.
Cantidad de 
registros 
incluidos
10003
La cantidad de registros en detalle debe ser menor igual al valor permitido. 
Consulte método FECompTotXRequest para obtener cantidad máxima de 
registros por cada requerimiento. Para comprobantes del tipo MiPyMEs 
(FCE), la cantidad habilitada es 1 comprobante por request
CbteTipo 700
Obligatorio.  Valores permitidos:
1: Factura A
2: Nota de Débito A
3: Nota de Crédito A



# Página 136

Campo / 
Grupo
Código de 
error Descripción de la validación
4: Recibo A
6: Factura B
7: Nota de Débito B
8: Nota de Crédito B
9: Recibo B
11: Factura C
12: Nota de Débito C
13: Nota de Crédito C
15: Recibo C
51: Factura M (CAEA observa comprobante)
52: Nota de Débito M (CAEA observa comprobante)
53: Nota de Crédito M(CAEA observa comprobante)
54: Recibo M
201: Factura de Crédito electrónica MiPyMEs (FCE) A
202: Nota de Débito electrónica MiPyMEs (FCE) A
203: Nota de Crédito electrónica MiPyMEs (FCE) A
206: Factura de Crédito electrónica MiPyMEs (FCE) B
207: Nota de Débito electrónica MiPyMEs (FCE) B
208: Nota de Crédito electrónica MiPyMEs (FCE) B
211: Factura de Crédito electrónica MiPyMEs (FCE) C
212: Nota de Débito electrónica MiPyMEs (FCE) C
213: Nota de Crédito electrónica MiPyMEs (FCE) C
Consultar método FEParamGetTiposCbte
PtoVta 1300 Campo PtoVta debe estar comprendido entre 1 y 99998.
PtoVta 701
El punto de Venta debe ser del tipo habilitado para CAEA - Fact. Elect. 
(RECE) - RI IVA / CAEA – Fact. Elect. (RECE) -  Contingencias / CAEA – Fact. 
Elect. (RECE) - Exento en IVA - Contingencias / CAEA – Fact. Elect. (RECE) - 
Monotributo - Contingencias y no debe estar bloqueado a la fecha en que 
se emitió el comprobante. Consultar método  FEParamGetPtosVenta.
Verificaciones que se realizan sobre el elemento < FECAEADetRequest>



# Página 137

Validaciones Excluyentes
Campo / Grupo Código de 
Error Validación
CbteFch 702 Debe estar comprendida dentro de la fecha desde 
y fecha hasta de vigencia del CAEA
CbteDesde / CbteHasta / PtoVta / 
CbteTipo 703
El número de comprobante informado debe ser 
mayor en 1 al último informado para igual punto 
de venta y tipo de comprobante. Consultar 
método FECompUltimoAutorizado
CbteFch / PtoVta / CbteTipo 704
La fecha del comprobante debe ser mayor o igual 
a la fecha del último comprobante informado para 
igual tipo de comprobante y punto de venta.
CAEA 705 Debe corresponder a la CUIT que está informando
Fecha de envío de la solicitud 1414
Al informar un comprobante con la modalidad 
CAEA, la fecha en la que se informa el 
comprobante debe ser mayor a la fecha de 
entrada en vigencia del CAEA vinculado
CAEA / PtoVta 709
La fecha de alta del  punto  de venta deberá ser 
menor o  igual a la fecha de vigencia “hasta” del  
CAEA
MonId 1401
El campo MonId es obligatorio  y debe 
corresponder a algún valor devuelto  por  el 
método FEParamGetTiposMonedas.
Concepto
713
Valores permitidos:
1 Productos
2 Servicios
3 Productos y Servicios
Consultar método FEParamGetTiposConcepto
ImpIVA / Iva / AlicIva 715
Si ImpIVA es igual a 0 los objetos Iva y AlicIva solo 
deben informarse con ImpIVA = 3 (iva 0)
Si ImpIVA es mayor a 0 el objeto Iva y AlicIva son 
obligatorios.
El objeto AlicIva es obligatorio y no debe ser nulo 
si ingresa Iva.
<ImpTotConc> 717 El campo ImpTotConc (Importe neto no gravado) 
no puede ser menor a cero (0).



# Página 138

Campo / Grupo Código de 
Error Validación
El campo ImpTotConc soporta 13 números para la 
parte entera y 2 para los decimales.
<ImpOpEx> 718
El campo ImpOpEx soporta 13 números para la 
parte entera y 2 para los decimales.
El campo ImpOpEx (importe exento) no puede ser 
menor a cero (0).
<ImpNeto> 719
El campo ImpNeto (Importe neto  gravado) no 
puede ser menor a cero (0)
El campo ImpNeto soporta 13 números para la 
parte entera y 2 para los decimales.
<ImpTrib> 723
El campo ImpTrib (Importe de tributos) no puede 
ser menor a cero (0).
El campo ImpTrib soporta 13 números para la 
parte entera y 2 para los decimales.
<ImpIVA> 1407
El campo ImpIVA (Importe de IVA) no puede ser 
menor a cero (0).
El campo ImpIVA soporta 13 números para la 
parte entera y 2 para los decimales. 
<MonCotiz> 726
El campo MonCotiz es obligatorio y mayor a 0
Debe ser igual a 1 (uno) si <MonId> es igual a PES. 
Si <MonId> es diferente a PES que <MonCotiz> sea 
Mayor a 0.
El campo MonCotiz es opcional  si  informa el 
campo CanMisMonExt con el valor S y el tipo de 
comprobante es factura y la moneda tiene 
cotización en Banco Nación.
El campo MonCotiz soporta 4 números para la 
parte entera y 6 para los decimales.
CAEA 780 Deberá corresponder a un CAEA registrado en las 
bases de la Administración
PtoVta / CbteFch 781 La fecha de alta del punto  de venta deberá ser 
menor o igual a la fecha del comprobante
CAEA 782 Obligatorio, numérico de 14 posiciones
CbteFch 783 Obligatorio, formato yyyymmdd
CbteDesde / CbteHasta 784 Obligatorio, entero; valores comprendidos entre 1 
y 99999999.
<CbteHasta> / <CbteDesde> 1416 Para comprobantes tipo B, <CbteHasta> sea mayor 



# Página 139

Campo / Grupo Código de 
Error Validación
o igual a <CbteDesde>
<CbteTipo> / <CbteDesde> / 
<CbteHasta> 1415
Para comprobantes tipo  B (CbteDesde distinto a 
CbteHasta) y el resultado de la operación ImpTotal 
/ (CbteHasta – CbteDesde + 1 ) < monto en pesos 
resultante según RG4444, el campo  DocNro 
deberá ser cero (0) y el campo 139DocTipo 99.
DocTipo / DocNro / CbteDesde / 
CbteHasta 1417
Para comprobantes B o C (CbteDesde igual a 
CbteHasta) mayor o igual a monto en pesos 
resultante según RG4444, DocTipo debe ser uno 
de los valores devueltos por el método 
FEParamGetTiposDoc distinto a 99 y DocNro 
deberá ser mayor a 0.
DocTipo / DocNro / CbteDesde / 
CbteHasta 1418
Para comprobantes B o C (CbteDesde igual a 
CbteHasta) menor a monto en pesos resultante 
según RG4444, si DocTipo = 99 DocNro debe ser 
igual a 0.
DocTipo / DocNro / CbteDesde / 
CbteHasta 1419
Para comprobantes B o C (CbteDesde igual a 
CbteHasta) menor a monto en pesos resultante 
según RG4444, si DocTipo es distinto a 99, DocNro 
debe ser mayor a 0.
<CbteTipo> / <CbteDesde> / 
<CbteHasta> 1422
Para comprobantes tipo B, <CbteDesde> distinto a 
<CbteHasta> el resultado de la operación 
ImpTotal / (CbteHasta – CbteDesde + 1 ) < monto 
en pesos resultante según RG4444.
<CbteTipo> / <CbteDesde> / 
<CbteHasta> 711
Para comprobantes clase A y comprobantes 
MiPyMEs (FCE) el campo CbteDesde debe ser igual 
al campo CbteHasta
<CbteTipo> / <DocTipo> 1403 Para comprobantes clase A el campo  DocTipo  
debe ser igual a 80 (CUIT)
<ImpTotal> 1409
El campo ImpTotal no puede ser menor a cero (0).
El campo ImpTotal soporta 13 números para la 
parte entera y 2 para los decimales.
<DocTipo> / <DocNro> 1404
Para comprobantes tipo B o tipo C, si informa 
<DocTipo> y <DocNro>, <DocTipo> debe ser un 
valor devuelto por el método 
FEParamGetTiposDoc.
<CbteTipo> / <DocNro> 1405
Para comprobantes tipo B o tipo C el campo  
DocNro debe ser un valor comprendido entre 0 y 
99999999999
<CbteTipo> / <DocNro> 1421 Para comprobantes tipo A el campo  DocNro debe 
ser un valor comprendido entre 20000000000 y 



# Página 140

Campo / Grupo Código de 
Error Validación
60000000000
DocTipo / DocNro 788
Cuando se informa tipo de comprobante 80, el 
documento informado no puede ser el mismo al 
ingresado en el campo <Auth><Cuit>
<ImpTrib> / <Tributos> / 
<Tributo> 1423
Si ImpTrib es igual a 0 el objeto Tributos y Tributo 
no deben informarse.
Si ImpTrib es mayor a 0 el objeto Tributos y 
Tributo son obligatorios.
Si ImpTrib mayor a 0, Tributos y Tributo no pueden 
venir vacíos.
<Opcionales><CbteTipo> 1426 El array <Opcionales> no es obligatorio. Solo 
puede informarse si <CbteTipo> es 1, 2, 3, 6, 7,8
<Compradores> 1432
No se encuentra habilitado informar compradores 
en el régimen de información para la modalidad 
CAEA. 
<CbteTipo>/
<CbteDesde>/
<CbteHasta>
1433
Para comprobantes tipo C <CbteHasta> debe ser 
igual a <CbteDesde>.
<CbteTipo>/
<ImpTotConc>
1434
Para comprobantes tipo C, el campo “Importe 
neto no gravado”  <ImpTotConc> debe ser igual a 
cero (0).
<CbteTipo>/
<ImpOpEx>
1435
Para comprobantes tipo C, el campo <ImpOpEx>  
debe ser igual a cero (0).
<CbteTipo>/
<ImpNeto>
1436
Para comprobantes tipo C el campo <ImpNeto> 
corresponde al Importe del Sub Total.
<CbteTipo>/
<ImpTrib>
1437
Para comprobantes tipo C, el campo “Importe de 
tributos”  <ImpTrib>. No puede ser menor a cero 
(0).
<CbteTipo>/
<ImpIVA>
1438
Para comprobantes tipo C, el campo “Importe de 
IVA” < ImpIVA> debe ser igual a cero (0).
<CbteTipo>/
<ImpTotal>/
<ImpNeto> /
<ImpTrib> / 
1439
Para comprobantes tipo C, el campo  “Importe 
Total” <ImpTotal>, debe ser igual  a la  suma de 
ImpNeto + ImpTrib.
Margen de error:
Error relativo porcentual deberá ser <= 0.01% o el 



# Página 141

Campo / Grupo Código de 
Error Validación
error absoluto <=0.01
<CbteFchHsGen> 1440 Si el punto de venta es para CONTINGENCIAS 
CAEA el campo es obligatorio informarlo
<CbteFchHsGen> 1441
Si informa el campo, el mismo tiene que contener 
un valor según lo definido en la estructura. 
Formato yyyymmddhhmiss
<Iva> 1443 Si el tipo de comprobante es C, el array de IVA no 
debe informarse.
<PtoVta> /<CbteTipo> 1444
Si el comprobante es tipo A, B, M los puntos de 
venta habilitados son CAEA - Fact. Elect. (RECE) - RI 
IVA / CAEA - Fact. Elect. (RECE) - RI IVA - 
Contingencias.
Si el comprobante es tipo C, los puntos de venta 
habilitados son CAEA - Fact. Elect. (RECE) - Exento 
en IVA – Contingencias /
CAEA - Fact. Elect. (RECE) - Monotributo - 
Contingencias
<FeCabReq><CbteTipo> / 
<CbteTipo> / <DocNro> 1445
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), el campo DocNro para 
comprobantes deberá ser un valor registrado en el 
padrón de arca, en condición activa.
<CbteTipo> / <DocNro> 1446
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), el campo DocNro para 
comprobantes deberá ser un valor registrado en el 
padrón de arca, en condición activa.
<FeCabReq><CbteTipo>/
<FECAEDetRequest><CbtesAsoc>
1450
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE) y corresponde a un comprobante 
de débito o crédito, es obligatorio informar 
comprobantes asociados.
<FeCabReq><CbteTipo>/
<CbteAsoc><Tipo><PtoVta><Nro
><Cuit>
1451
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE) y corresponde a un comprobante 
de débito o crédito. Tener en cuenta que:
- sí el comprobante asociado  se encuentra 
rechazado por el comprador hay que informar el 
código de anulación correspondiente sobre el 
campo "Adicionales por RG",  códigos 22  -  
Anulación. Valor “S”
- sí el comprobante asociado  no se encuentra 
rechazado por el comprador hay que informar el 
código de no anulación correspondiente sobre el 
campo "Adicionales por RG",  códigos 22  -  



# Página 142

Campo / Grupo Código de 
Error Validación
Anulación. Valor “N”
<Auth><Cuit>
<FeCabReq><CbteTipo>/
<CbteAsoc><Cuit>
1452
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), el CUIT del emisor del 
comprobante asociado debe coincidir con el CUIT 
del emisor del comprobante a autorizar. 
<FeCabReq><CbteTipo>/
<CbteAsoc><Tipo>
1453
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), Débito o Crédito sin código de 
Anulación siempre debe asociar 1 solo 
comprobante tipo factura.
<FeCabReq><CbteTipo>/
<CbteAsoc><Tipo>
1454
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), Débito o Crédito sin código de 
Anulación solo puede asociar:
Para comprobantes A, asociar 201 o  (91, 88, 988, 
990, 991, 993, 994, 995, 996, 997). 
Para comprobantes B, asociar 206 o  (91, 88, 988, 
990, 991, 993, 994, 995, 996, 997).
Para comprobantes C, asociar 211 o  (91, 88, 988, 
990, 991, 993, 994, 995, 996, 997).
<FeCabReq><CbteTipo>/
<CbteAsoc> / <Tipo> / <PtoVta> /
<Nro> / <Cuit> / <CbteFch>
1455
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), Débito o Crédito, es obligatorio 
informar la fecha del comprobante asociado
<FeCabReq><CbteTipo>/
<FeDetReq>/<CbteFch>/
<CbteAsoc> / <Tipo> / <PtoVta> /
<Nro> / <Cuit> / <CbteFch>
1456
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), Débito o Crédito, la fecha del 
comprobante asociado tiene que ser igual o 
menor a la fecha del comprobante que se está 
autorizando
<FeCabReq><CbteTipo>/
<CbteAsoc> / <Tipo> / <PtoVta> /
<Nro> / <Cuit> / <CbteFch>
1457
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), Débito o Crédito, el comprobante 
debe existir autorizado en las bases de esta 
Administración con la misma fecha informada en 
el asociado. 
<FeCabReq><CbteTipo>/
<FECAEDetRequest><DocTipo><
DocNro>
1458
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), el receptor del comprobante debe 
tener habilitado el domicilio fiscal electrónico
<FeCabReq><CbteTipo>/
<Opcionales>
1459
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE) 
es obligatorio informar <Opcionales>



# Página 143

Campo / Grupo Código de 
Error Validación
<FeCabReq><CbteTipo>/
<FchVtoPago>
1460
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), Tipo 201 - FACTURA DE CREDITO 
ELECTRONICA MiPyMEs (FCE) A / 206 - FACTURA 
DE CREDITO ELECTRONICA MiPyMEs (FCE) B / 211 
- FACTURA DE CREDITO ELECTRONICA MiPyMEs 
(FCE) C,  es obligatorio informar FchVtoPago
<FeCabReq><CbteTipo>/
<FchVtoPago> / 
<FECAEDetRequest><CbteFch>
1461
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), la fecha de vencimiento de pago  
(FchVtoPago) debe ser posterior o igual a la fecha 
de emisión (CbteFch) o fecha de presentación (fe-
cha actual), la que sea posterior
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1462
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), informa opcionales, el valor co-
rrecto para el código 2101 es un CBU numérico de 
22 caracteres.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1463
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), informa opcionales, el valor co-
rrecto para el código 2102 es un ALIAS alfanuméri-
co de 6 a 20 caracteres.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1464
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), informa opcionales, el valor co-
rrecto para el código 22 es “S” o “N”:
S = Es de Anulación
N = No es de Anulación
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1465
Si el tipo de comprobante que está autorizando es 
Factura (201, 206, 211) del tipo MiPyMEs (FCE), in-
forma opcionales, es obligatorio informar CBU.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1466
Si el tipo de comprobante que está autorizando 
NO es MiPyMEs (FCE), no informar los códigos 
2101, 2102, 22, 27
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1467
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), es obligatorio informar al menos 
uno de los sig. códigos 2101, 22, 27
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1468
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), Factura (201, 206, 211), no infor-
mar Código de Anulación 
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1469
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), Debito (202, 207, 212) o Crédito 
(203, 208, 213) No informar CBU y ALIAS.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1470
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), Debito (202, 207, 212) o Crédito 
(203, 208, 213) informar Código de Anulación
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1471
Si el tipo de comprobante que está autorizando es 
factura MiPyMEs (FCE), el CBU debe estar registra-
do en las bases de esta administración, vigente y 
pertenecer al emisor del comprobante.



# Página 144

Campo / Grupo Código de 
Error Validación
<FeCabReq><CbteTipo>/
<FchVtoPago> 
1472
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), el campo “fecha de vencimiento 
para el pago”  <FchVtoPago>  no debe informarse 
si NO es Factura de Crédito. En el caso de ser 
Débito o Crédito, solo puede informarse si es de 
Anulación.
<FeCabReq><CbteTipo>/
<CbteTipo> / <DocNro>
1474
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE) del tipo A, el receptor del 
comprobante informado en DocTipo y  DocNro 
debe corresponder a un contribuyente activo en el 
Impuesto al Valor Agregado o Responsable 
Monotributo.
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE) del tipo B, el receptor del 
comprobante informado en DocTipo y  DocNro 
debe corresponder a un contribuyente activo en el 
Impuesto Iva, Monotributo o Exento.
Si el tipo de comprobante que esta autorizando es 
MiPyMEs (FCE) del tipo C, el receptor del 
comprobante informado en DocTipo y  DocNro 
debe corresponder a un contribuyente activo en el 
Impuesto Iva, Monotributo o Exento.
<FeCabReq><CbteTipo>/
<CbteTipo> / <DocNro>
1475
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE) no se permite informar DocNro 
23000000000 (No Categorizado)
<FeCabReq><CbteTipo>/
<CbteTipo> / <DocNro>
1476
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), el receptor del comprobante 
informado en DocTipo y  DocNro debe 
corresponder a un contribuyente caracterizado 
como GRANDE o que opto por PYME. Su activida 
principal debe corresponderse con alguna de las 
alcanzadas por el régimen. 
<FeCabReq><CbteTipo>/
<MonId>
<CbteAsoc> 1477
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), débito o crédito, el mismo debe 
tener la misma moneda que el comprobante aso-
ciado o Pesos para ajuste en las diferencias de 
cambio (post aceptación/rechazo)
<FeCabReq><CbteTipo>/
<FECAEDetRequest><DocTipo><
DocNro>/
<CbteAsoc>
1478
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), es débito o crédito, deben 
coincidir emisores y receptores.
Si el comprobante ES de anulación, para autorizar 
un débito, el tipo de comprobante a asociar debe 



# Página 145

Campo / Grupo Código de 
Error Validación
ser crédito y para autorizar un crédito, el tipo de 
comprobante a asociar debe ser una factura o un 
débito.
<FeCabReq><CbteTipo>/
<MonId>
<CbteAsoc>/
<FeCabReq><ImpTotal>
1479
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), es crédito el monto del 
comprobante a autorizar no puede ser mayor o 
igual al saldo actual de la cuenta corriente. Ver 
micrositio factura de crédito
<FeCabReq><CbteTipo>/
<CbteAsoc>/ 1480
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), es crédito o débito A, de 
anulación, solo se encuentra habilitado asociar un 
comprobante de crédito A. Utilizar debito para 
anular crédito o utilizar crédito para anular débito 
o factura. 
<FeCabReq><CbteTipo>/
<CbteAsoc>/ 1481
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), es crédito o débito B, de 
anulación, solo se encuentra habilitado asociar un 
comprobante de crédito B. Utilizar debito para 
anular crédito o utilizar crédito para anular débito 
o factura.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1482
Puede identificar una o varias Referencias 
Comerciales según corresponda. Informar bajo el 
código 23. Campo alfanumérico de 50 caracteres 
como máximo.
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1483
Si informa opcionales con más de un identificador 
23 – Referencia Comercial, no repetir el valor. 
<FeCabReq><CbteTipo>/
<CbteAsoc>/ 1486
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), es crédito o débito C, de 
anulación, solo se encuentra habilitado asociar un 
comprobante de crédito C. Utilizar debito para 
anular crédito o utilizar crédito para anular débito 
o factura.
<CbteTipo> / <DocTipo> / 
<DocNro> 1487 Para comprobantes MiPyMEs (FCE) el documento 
del receptor debe ser 80 CUIT.
<FeCabReq><CbteTipo>/
<CbteAsoc> / <Tipo> / <PtoVta> /
<Nro> / <Cuit> / <CbteFch>
1488
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), Débito o Crédito, el comprobante 
debe existir autorizado en las bases de esta 
Administración. 
<FeCabReq><CbteTipo>/
<FECAEADetRequest><PeriodoAs
oc>
1490
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), no se encuentra habilitado 
informar PeriodoAsoc.



# Página 146

Campo / Grupo Código de 
Error Validación
<FeCabReq><CbteTipo>/
<FECAEADetRequest><CbtesAsoc
>/
<FECAEADetRequest><PeriodoAs
oc>
1491
Si el comprobante es Debito o Crédito, se deberá 
informar de forma obligatoria los campos Fecha 
Comprobantes Asociados Desde/Hasta, o al menos 
un comprobante asociado.
<FeCabReq><CbteTipo>/
<FECAEADetRequest><PeriodoAs
oc>
1492
Si el comprobante es Factura no se deberá 
informar los campos Fecha Comprobantes 
Asociados Desde/Hasta
<FECAEADetRequest><PeriodoAs
oc><FchDesde> 1493 Si envía estructura PeriodoAsoc es obligatorio 
enviar FchDesde.
<FECAEADetRequest><PeriodoAs
oc><FchHasta> 1494 Si envía estructura PeriodoAsoc es obligatorio 
enviar FchHasta.
<FECAEADetRequest><PeriodoAs
oc><FchDesde> 1495
El campo PeriodoAsoc.FchDesde debe 
corresponder a una fecha valida con formato 
YYYYMMDD
<FECAEADetRequest><PeriodoAs
oc><FchHasta> 1496
El campo PeriodoAsoc.FchHasta debe 
corresponder a una fecha valida con formato 
YYYYMMDD
<FECAEADetRequest><PeriodoAs
oc><FchDesde>/
<FECAEADetRequest><PeriodoAs
oc><FchHasta>
1497
Las fechas informadas en PeriodoAsoc deben ser 
superiores a 01/01/2006
<FECAEADetRequest><PeriodoAs
oc><FchDesde>/
<FECAEADetRequest><PeriodoAs
oc><FchHasta>
1498
Las fechas informadas en PeriodoAsoc , FchHasta 
debe ser superior o igual a FchDesde.
<FECAEADetRequest><PeriodoAs
oc><FchHasta> 1499
Las fecha informada en PeriodoAsoc.FchHasta 
debe ser anterior o igual a la fecha de emisión del 
comprobante que estamos autorizando 
<FECAEADetRequest 
><CbtesAsoc><CbteFch> 1502
Informar de forma obligatoria la fecha de Emisión 
del comprobante asociado si el punto de venta del 
comprobante asociado es Controlador Fiscal o 
FactuWeb y el tipo de Comprobante asociado es 
Factura, Recibo, Nota de Débito/Nota de Crédito
<FECAEADetRequest 
><CbtesAsoc><CbteFch> 1503
De informar fecha de Emisión del comprobante 
asociado y el punto de venta es Controlador Fiscal 
o FactuWeb, la fecha no puede ser posterior al día 
de hoy.
<FECAEADetRequest><CbtesAsoc 1504 Si se informan deben tener el siguiente formato 



# Página 147

Campo / Grupo Código de 
Error Validación
><CbteFch> yyyymmdd.
<FECAEADetRequest>/
<Opcionales><Id>/<CbteTipo>
1505
Si el comprobante es del tipo A (1, 2, 3, 4, 5, 34, 
39, 60, 63) e intenta informar datos opcionales 
según Resolución General 3668, los valores 
posibles para los identificadores son 5, 61, 62, 7.
<FECAEADetRequest>/
<Opcionales><Id>/
<Opcionales><Valor>
1506
Si informa Id = 5, el valor ingresado no puede ser 
blanco y debe ser alfanumérico de 2 caracteres.
<FECAEADetRequest>/
<Opcionales><Id>/
<Opcionales><Valor>
1507
Si informa Id = 5, el contenido del campo <Valor> 
debe corresponder a un código de EXCEPCION 
válido comprendido por alguno de los sig:
01 – Locador / Prestador del mismo
02 – Congresos / Eventos
03 – Operación contemplada en RG 74
04 – Bienes de Cambio
05 – Ropa de trabajo
06 – Intermediario 
<FECAEADetRequest>/
<Opcionales><Id>/
<Opcionales><Valor>
1508
Si informa Id = 61, el valor ingresado no puede ser 
blanco y debe ser numérico de 2 caracteres.
<FECAEADetRequest>/
<Opcionales><Id>/
<Opcionales><Valor>
1509
Si informa Id = 61, el contenido del campo <Valor> 
debe corresponder a un código que represente el 
tipo de documento del firmante. Ver método 
FEParamGetTiposDoc.
<FECAEADetRequest>/
<Opcionales><Id>/
<Opcionales><Valor>
1510
Si informa Id = 62, el valor ingresado no puede ser 
blanco y debe ser numérico de 11 caracteres como 
máximo.
<FECAEADetRequest>/
<Opcionales><Id>/
<Opcionales><Valor>
1511
Si informa Id = 7, el valor ingresado no puede ser 
blanco y debe ser numérico de 2 caracteres.
<FECAEADetRequest>/
<Opcionales><Id>/
<Opcionales><Valor>
1512
Si informa Id = 7, el contenido del campo <Valor> 
debe corresponder a un código de carácter 
firmante válido comprendido por alguno de los sig:
01 – Titular



# Página 148

Campo / Grupo Código de 
Error Validación
02 – Director / Presidente
03 – Apoderado
04 – Empleado
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1513
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), informa opcionales, el tipo de 
dato correcto para el código 27 es un 
alfanumérico de 3 caracteres. 
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1514
Si el tipo de comprobante que está autorizando es 
MiPyMEs (FCE), informa opcionales y el código es 
27, los valores posibles son:
SCA = "TRANSFERENCIA AL SISTEMA DE 
CIRCULACION ABIERTA"
ADC = "AGENTE DE DEPOSITO COLECTIVO"
<FeCabReq><CbteTipo>/
<Opcionales><Id><Valor>
1515
Si el tipo de comprobante que está autorizando es 
Factura del tipo MiPyMEs (201, 206, 211),  es 
obligatorio informar <Opcionales> con id = 27. Los 
valores posibles son SCA o ADC.
<FECAEADetRequest> / 
<CanMisMonExt> 820 Si informa el campo CanMisMonExt, los valores 
posibles son S o N y no debe quedar vacío.
<FECAEADetRequest> / 
<CondicionIVAReceptorId> 823
El campo de identificación de la Condición de IVA 
del receptor no es un valor permitido. Para mayor 
detalle consular el método 
FEParamGetCondicionIvaReceptor.
<FECAEADetRequest> / 
<CondicionIVAReceptorId> 826
Campo Condición Frente al IVA del receptor es 
obligatorio conforme a lo reglamentado por la 
Resolución General N° 5616. Para mas información 
consular método 
FEParamGetCondicionIvaReceptor.
Validaciones NO Excluyentes
Campo Código de 
Observ. Validación
<CbteTipo> / <DocNro> 708
El campo DocNro para comprobantes Tipo A 
deberá ser un valor registrado y ACTIVO en el 
padrón de arca.
<ImpTotConc> / <ImpOpEx> / 
<ImpNeto> / <ImpTrib> / 
724 El campo  “Importe Total” <ImpTotal>, debe ser 
igual  a la  suma de ImpTotConc + ImpNeto + 



# Página 149

Campo Código de 
Observ. Validación
<ImpIVA> / <ImpTotal>
ImpOpEx + ImpTrib + ImpIVA
Margen de error:
Error relativo porcentual deberá ser <= 0.01% o el 
error absoluto <=0.01
FchServHasta 728 Debe informarse solo si <Concepto> es igual a 2 ó 
3. En otro caso no corresponde.
<ImpIVA> 725
Debe ser igual a la sumatoria de la totalidad de los 
campos <importe> (dentro de <AlicIVA>)
Margen de error:
Error relativo porcentual deberá ser <= 0.01% o el  
error absoluto <=0.01 * cantidad de alícuotas de  
IVA  ingresadas*
<CbteTipo> / 149<DocTipo> / 
<DocNro> 1402
Para comprobantes Tipo A deberá encontrarse 
registrado en condición activa en el Impuesto al 
Valor Agregado o Responsable Monotributo.
<FchServDesde> 727 FchServDesde debe informarse solo si Concepto es 
igual a 2 o 3. En otro caso no corresponde.
<CbteTipo> / <DocTipo> / 
<DocNro> 1420
Para comprobantes tipo B o C (CbteDesde igual a 
CbteHasta) y DocTipo 80, 86, 87, DocNro deberá 
ser un valor registrado en el padrón de arca. Si 
DocTipo es 80 y DocNro es 23000000000 (No 
Categorizado) esta validación no se tendrá en 
cuenta.
<ImpNeto> / <AlicIva> <BaseImp> 1408
La suma de los campos <BaseImp> en  <AlicIva>   
debe  ser igual al valor ingresado  en  ImpNeto. 
Esta validación no deberá ser tenida en cuenta, 
cuando el <CbteTipo> sea 02, 03, 07, 08.
Margen de error:
Error relativo porcentual deberá ser <= 0.01% o el 
error absoluto <=0.01 * cantidad de alícuotas de 
IVA ingresadas *
FchVtoPago 1411 Debe ser mayor o igual a la fecha del comprobante.
FchVtoPago 729 Debe informarse solo si <Concepto> es igual a 2 ó 
3. En otro caso no corresponde.
<FchServDesde>/ <FchServHasta> 1412 <FchServDesde> no puede ser posterior al campo 
<FchServHasta>.
<ImpTrib> 1406 Debe ser igual a la sumatoria de la totalidad de los 



# Página 150

Campo Código de 
Observ. Validación
campos <Importe> (dentro de <Tributos>).
Margen de error:
Error relativo porcentual deberá ser <= 0.01% o el  
error absoluto <=0.01 * cantidad de tributos *
CAEA /  <PtoVta> 1424 El CAEA y punto de venta no debe estar informado 
sin movimientos.
<ImpTrib> <DocTipo><DocNro> 1425
Para comprobantes tipo B, si <DocTipo> es 80 y 
<DocNro> es 23000000000 (No Categorizado), 
<ImpTrib> debe ser mayor a 0.
<FchServDesde>/ 
<FchServHasta>/ <FchVtoPago> 1413 Si se informan deben tener el siguiente formato 
yyyymmdd.
<ImpNeto>/
<Iva>.<AlicIva>
1427 Si ImpNeto es mayor a 0, el objeto AlicIva es 
obligatorio y no debe ser nulo.
<Auth><Cuit> /
<CbteTipo> /
<CbteFch>
1429
No se encuentra habilitado a emitir comprobantes 
“A” a la fecha de emisión del comprobante. El 
comprobante  queda observado. 
<Auth><Cuit> /
<CbteTipo> /
<CbteFch>
1431 Al momento de emitir el comprobante, debe estar 
dado de alta en el Impuesto.
<CbteFchHsGen> 1442
Si el punto de venta informado no es para 
CONTINGENCIA no informar el campo 
<CbteFchHsGen>
<CbteFch>/
<CbteFchHsGen>/
<Concepto>
1445
El campo <CbteFch> podrá estar  comprendido  en 
el  rango  N-5 y N+5 siendo  N la fecha 
CbteFchHsGen (contingencia) para Concepto= 01 
Productos. 
-  Para Concepto 02, 03 el  campo CbteFch debe 
estar  comprendido  en el  rango  N-10 y N+10 
siendo N la fecha CbteFchHsGen (contingencia). 
<Auth><Cuit>/
<FeCabReq><CbteTipo>/
<FECAEDetRequest><DocNro>/
<FECAEDetRequest><ImpTotal> / 
<FECAEDetRequest><MonCotiz> 
/ Tope
1485
Según la categorización de las CUITs emisora y 
receptora y el monto facturado debe realizar una 
factura  de crédito electrónica MiPyMEs (FCE). Ver 
micrositio.
<DocTipo>/ 1489 Si el tipo de documento del receptor del 



# Página 151

Campo Código de 
Observ. Validación
<DocNro>/
comprobante que está autorizando es CUIT (código 
Tipo de Documento 80) y la CUIT se encuentra 
inactiva por haber sido incluida en la consulta de 
facturas apócrifas, no podrá computarse el crédito 
fiscal.
<FECAEADetRequest><Tributos><
Id>/
<FECAEADetRequest><PeriodoAs
oc><FchDesde>/
<FECAEADetRequest><PeriodoAs
oc><FchHasta>
1500
Si en la estructura Tributos informa percepciones, 
PeriodoAsoc.FchDesde y PeriodoAsoc.FchHasta 
deben corresponder al mismo Mes/Anio
<FECAEADetRequest><CbteFch>/
<FECAEADetRequest><CbtesAsoc
><CbteFch>
1501
Si el comprobante asociado se autorizó de forma 
electrónica y tiene una fecha de emisión posterior 
a la fecha de emisión del comprobante por el cual 
se está solicitando la autorización, ambos deberán 
ser del mismo mes/año.
<CbteTipo> / <DocNro> 1516
Para comprobantes Clase A y M, donde el receptor 
del comprobante informado en <DocTipo> y  
<DocNro> se encuentra activo en el Impuesto 
Responsable Monotributo, “El crédito fiscal 
discriminado en el presente comprobante solo 
podrá ser computado a efectos del Procedimiento 
permanente de transición al Régimen General.”
<CbteTipo> / <DocNro> 814
Para comprobantes Clase A y M, se ha detectado 
que esta pendiente de presentación el formulario 
de habilitación de comprobantes o su fecha de 
presentación es anterior a la fecha de alta en IVA. 
Para el caso que el comprobante no sea una Nota 
de Crédito, se debe proceder a anular la operación 
clase "A" o "M" emitida, mediante una Nota de 
Crédito.
<CbteTipo> / <DocNro> / 
<FECAEADetRequest><CbteFch>/ 815
A la fecha de emisión del comprobante, no te 
encontrabas habilitado a la emisión de 
comprobantes clase "A"/"M". Tenés que proceder 
a emitir la Nota de Crédito, o anular la operación, 
según corresponda.
<CbteTipo> / <DocNro> / 
<FECAEADetRequest> / ImpTotal 816
El monto total del comprobante emitido, excede el 
límite establecido para la categoría máxima de 
Monotributo. Por tal motivo, quedarías excluido 
automáticamente teniendo que solicitar el alta de 
los tributos (impositivos y de los recursos de la 
seguridad social) en el régimen general de acuerdo 



# Página 152

Campo Código de 
Observ. Validación
con tu actividad.
<CbteTipo> / <DocNro> / 
<FECAEADetRequest> / ImpTotal 817
El monto del comprobante emitido, excede el 
límite establecido para su categoría de 
Monotributo. Tenelo en cuenta para la próxima 
recategorización.
<FECAEADetRequest> / 
ImpTotal / <CbteAsoc> 818
El importe de la nota de crédito supera el monto 
del comprobante asociado que estás ajustando. 
Verificá los montos ingresados y de tratarse de un 
error, tenés que efectuar el ajuste o anulación de 
la operación según corresponda.
<FECAEADetRequest> / <DocNro> 819 La CUIT receptora ingresada no existe.
<FECAEADetRequest> / 
<CanMisMonExt> 821
Si informa el campo MonCotiz, el mismo no podrá 
superar en 1 a la cotizacion oficial. Ver Método 
FEParamGetCotizacion.
<FECAEADetRequest> / 
<CanMisMonExt> 822 Si informa MonId = PES, el campo CanMisMonExt 
no debe informarse o informarse con el valor N.
<FECAEADetRequest> / 
<CondicionIVAReceptorId> 824
El campo de identificación de Condición de IVA del 
receptor no es valido para la clase de comprobante 
informado. Para mas detalle consultar el Método: 
FEParamGetCondicionIvaReceptor 
<FECAEADetRequest> / 
<CondicionIVAReceptorId> 825
El campo Condición Frente al IVA del receptor 
resultará obligatorio conforme lo reglamentado 
por la Resolución General N° 5616. Para mas 
información consular método 
FEParamGetCondicionIvaReceptor.
Verificaciones que se realizan sobre el elemento <CbtesAsoc>
Validaciones Excluyentes
Campo Código de Error Validación
CbtesAsoc 800
Si envía CbtesAsoc, CbteAsoc es obligatorio y 
no debe estar vacío.
PtoVta 802 De enviarse el tag CbtesAsoc, CbteAsoc debe 
enviarse con PtoVta mayor a 0 y < a 99999
Nro 803 De enviarse el tag CbtesAsoc, CbteAsoc debe 



# Página 153

enviarse con Nro mayor a 0 y menor a 
99999999
Tipo / PtoVta / Nro 804 Los comprobantes informados no podrán 
repetirse.
Tipo 805 De enviarse el tag CbtesAsoc, CbteAsoc debe 
enviarse con Tipo mayor a 0
CbteTipo / CbtesAsoc 807
CbtesAsoc es opcional, solamente podrá 
informarse si CbteTipo es igual a 1, 2, 3, 6, 7, 
8, 12, 13, 51, 52, 53, 201, 202, 203, 206, 207, 
208, 211, 212, 213. 
<CbteAsoc><Cuit> 808
Si informa Cuit en comprobantes asociados, 
no informar en blanco, el mismo debe ser un 
valor de 11 caracteres numericos. Para 
comprobante del tipo MiPyMEs (FCE) del tipo 
débito o crédito es obligatorio informar el 
campo.
CbteTipo / CbtesAsoc 812
Para comprobantes MiPyMEs (FCE) 201, 206 
o 211 puede asociarse los comprobantes (91, 
990, 991, 993, 994, 995). 
Para comprobantes MiPyMEs (FCE) A 202 y 
203, puede asociar 201,202 o 203.
Para comprobantes MiPyMEs (FCE) B 207, 
208 puede asociar 206, 207, 208.
Para comprobantes MiPyMEs (FCE) C 212, 
213 puede asociarse 211, 212, 213.
<Opcionales><Id>
813
Si intenta informar datos opcionales según 
Resolución General, recordar que en un 
mismo comprobante solo puede informar 
identificadores opcionales para solo 1 
resolución por comprobante.
Validaciones NO Excluyentes
Campo Código de Observ. Validación
Tipo 806
Obligatorio.  Deberá ser igual a 1, 2, 3, 04, 05, 34, 
39, 60, 63, 88 o 991  si el tipo de comprobante 
que se informa es igual a 2 ó 3.
Deberá ser igual a 6, 7, 8, 09, 10, 35, 40, 61, 64 ,88 



# Página 154

o 991 si el tipo de comprobante que se informa es 
igual a 7 ú 8.
Deberá ser igual a 11, 12, 13, 15 si el tipo de 
comprobante que informa es igual a 12 o 13
Deberá ser igual a 51, 52, 53, 54  si el tipo de 
comprobante que se informa es igual a 52 o 53. 
Deberá ser 91, 88, 988, 990, 991, 993, 994, 995, 
996, 997 si el tipo de comprobante que se informa 
es 1, 6 o 51
Tipo/ PtoVta / Nro 801
Si el punto de venta del comprobante asociado 
(campo PtoVta de CbtesAsoc) es electrónico, el 
número de comprobante debe obrar en las bases 
del organismo para el punto de venta y tipo de 
comprobante informado.
<CbteAsoc><Tipo> / 
<CbteAsoc><PtoVta> / 
<CbteAsoc><Nro> 809
Si informa comprobantes asociados, y sus códigos 
son 91, 88, 988, 990, 991, 993, 994, 995, 996, 997, 
los mismos deben encontrarse registrados.
<CbteAsoc><Tipo> / 
<CbteAsoc><PtoVta> / 
<CbteAsoc><Nro> 810
Si informa comprobantes asociados, y sus códigos 
son 91, 88, 988, 990, 991, 993, 994, 995, 996, 997, 
los mismos deben encontrarse confirmados.
<DocTipo> / <DocNro>
<CbteAsoc><Cuit>
811
Si informa comprobantes asociados y sus códigos 
son 91, 88, 988, 990, 991, 993, 994, 995, 996, 997, 
el receptor del comprobante debe ser igual al 
receptor del comprobante asociado.
Controles que se realizan sobre el elemento <Tributo>
Validaciones Excluyentes
Campo Código de Error Validación
Id 900 Obligatorio. Valores permitidos: consultar método 
FEParamGetTiposTributos
Desc 908 Opcional.  Debe informarse si <codigo> es igual a 99.



# Página 155

Campo Código de Error Validación
Importe 907
El valor informado debe ser mayor o igual a 0.
El campo Importe de Tributos soporta 13 números para 
la parte entera y 2 para los decimales.
BaseImp 905
El campo BaseImp en Tributo es obligatorio, mayor o 
igual 0 cero.
El campo BaseImp de Tributos soporta 13 números para 
la parte entera y 2 para los decimales.
Alic 906
El campo Alic en Tributo es obligatorio, mayor o igual  0 
cero.
El campo Alic de Tributos soporta 3 números para la 
parte entera y 2 para los decimales.
Controles que se realizan sobre el elemento <IVA>
Validaciones Excluyentes
Campo Código de Error Validación
Id 1000 Consultar el método FEParamGetTiposIva. Es opcional 
para comprobantes 2, 3, 7, 8.
Id 1003 El  campo  Id en AlicIVA no debe repetirse. Deberá 
totalizarse por alícuota.
Importe 1008
El  campo  Importe  en AlicIVA es obligatorio , mayor o 
igual  0 cero.
El campo Importe de AlicIva soporta 13 números para la 
parte entera y 2 para los decimales.
BaseImp 1009
El  campo  BaseImp  en AlicIVA es obligatorio  y debe ser 
mayor a 0 cero. Excepto para comprobantes 2, 3, 7, 8 
que puede ser cero o no ser informado.
El campo BaseImp de AlicIva soporta 13 números para 
la parte entera y 2 para los decimales.
Validaciones NO Excluyentes



# Página 156

Campo Código de 
Observ. Validación
Importe / AlicIva / 
BaseImp 1006
Los importes informados en AlicIVA no se corresponden 
con los porcentajes. Excepto para comprobantes 2, 3, 7, 
8 que puede ser cero o no ser informado.
Margen de error:
Error relativo porcentual deberá ser <= 0.01% o el error 
absoluto <=0.01
Controles que se realizan sobre el elemento <Opcionales>
Validaciones Excluyentes
Campo Código de Error Validación
Id 1100 El  campo Id en Opcionales es obligatorio  y debe ser 
igual a 2 (Régimen de Promoción Industrial).
Id 1101 El  campo Id en Opcionales es obligatorio  y no debe 
repetirse.
Valor 1105 El  campo Valor en Opcionales es obligatorio.
<Opcionales><Opcional
><Id><Valor> 1103 Si envía Opcionales, Opcional, Id y Valor son 
obligatorios.
Valor 1104 Si selecciona Id = 2 el valor ingresado debe ser un 
numérico de 8 (ocho) dígitos mayor o igual a 0 (cero). 
Validaciones NO Excluyentes
Campo Código de 
Observ. Validación
Valor 1106
Si Id = 2 y el comprobante corresponde a una actividad 
alcanzada por el beneficio de Promoción Industrial en el 
campo <Valor> se deberá informar el número 



# Página 157

identificatorio del proyecto (el mismo deberá corresponder 
a la cuit emisora del comprobante), si no corresponde a 
una actividad alcanzada por el beneficio el campo <Valor> 
deberá ser 0 (cero).
Controles que se realizan sobre el elemento <Actividades>
Validaciones Excluyentes
<FeCAEARegInfReq><Actividades><Actividad> 16000 Si envía estructura de Actividades, 
Actividad es obligatorio enviarlo.
<FeCAEARegInfReq><Actividades><Actividad><Id> 16001
Si envía estructura de Actividades, 
Actividad es obligatorio enviarlo y no 
debe estar vacío.
<FeCAEARegInfReq><Actividades><Actividad><Id> 16002
El identificador de actividad 
informado tiene que ser una de las 
actividades habilitadas. Consultar 
método FEParamGetActividades.
<FeCAEARegInfReq><Actividades><Actividad><Id> 16003 De enviarse el tag Actividades, las 
actividades no deben repetirse.
<FeCAEARegInfReq><Actividades><Actividad><Id> 16004
De enviarse actividades, las mismas 
no deben corresponder a distintos 
grupos según RG. Es decir, si informa 
actividades Cárnicas, no pueden estar 
combinadas con actividades 
Harineras, de Tabaco, etc.
<FeCAEARegInfReq><Actividades><Actividad><Id> 16005
De enviarse actividades, las mismas 
deben encontrarse activas para el 
emisor del comprobante. Ver método 
FEParamGetActividades.
<FeCAEARegInfReq><Actividades><Actividad><Id> / 
<Concepto> 16006
De enviarse actividades 
pertenecientes al grupo de 
actividades Cárnicas, las mismas 
deben enviarse con comprobantes 
con Concepto del tipo Producto o 
Productos y Servicios.



# Página 158

Controles que se realizan sobre el elemento <Actividades> y <CbtesAsoc>
Validaciones Excluyentes
<FeCAEARegInfReq><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc><Tipo>
16007
Si las actividades informadas 
corresponden a actividades Cárnicas, 
informar remito asociado 995 - 
Remito Electrónico Cárnico.
<FeCAEARegInfReq><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc><Tipo>
16008
Si las actividades informadas 
corresponden a actividades Harinas, 
informar remito asociado 993 - 
Remito Electrónico Harinero - 
Automotor o 
994 - Remito Electrónico Harinero - 
Ferroviario.
<FeCAEARegInfReq><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc><Tipo>
16009
Si las actividades informadas 
corresponden a actividades Tabaco 
en Hebras, informar remito asociado 
88 - Remito Electrónico de Tabaco 
Acondicionado.
<FeCAEARegInfReq><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc><Tipo>
16010
Si informa remito 995 - Remito 
Electrónico Cárnico, es obligatorio 
informar una actividad Cárnica.
<FeCAEARegInfReq><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc><Tipo>
16011
Si informa remito 993 - Remito 
Electrónico Harinero - Automotor o 
994 - Remito Electrónico Harinero – 
Ferroviario, es obligatgorio informar 
una actividad Harinera.
<FeCAEARegInfReq><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc><Tipo>
16012
Si informa remito 88 - Remito 
Electrónico de Tabaco 
Acondicionado., es obligatorio 
informar una actividad 
correspondiente a Tabaco 
Acondicionado.
<FeCAEARegInfReq><Actividades><Actividad><Id>
/<CbtesAsoc><CbteAsoc
16013
Si informa actividades indicadas en 
la RG, es obligatorio informar 
comprobantes asociados.
<CbteAsoc><Tipo> / <CbteAsoc><PtoVta> / 
<CbteAsoc><Nro> 16014
Si informa comprobantes asociados, 
y sus códigos corresponden a 91, 88, 
988, 990, 991, 993, 994, 995, 996, 
997, los mismos no deben 
encontrarse asociado a otro 
comprobante.



# Página 159

Operatoria ante errores
Metodología sugerida ante el rechazo de un requerimiento con múltiples comprobantes: 
Suponiendo  que se envían 100 comprobantes en un request y  el  mismo  es de Facturas A, 
punto  de venta 1 y los comprobantes son desde el número  51 al  150, se nos plantean 3 
situaciones.
 Aceptación total: donde, cada uno de  los 100  comprobantes fue aprobado. El campo 
Resultado será igual A
 Rechazo  total: se puede dar por  dos grandes causas, una por problemas del emisor 
y/o inconsistencia en la cabecera, y otra por el  rechazo  de cada uno de los 100 
comprobantes. En el primer caso el response contendrá solamente en  el  tag Errors 
con todas las causas involucradas; en el  segundo  caso se incluirá el tag FeCabResp, 
FeDetResp y Observaciones o Errors con el  motivo de rechazo de cada uno de los 
comprobantes.  El campo Resultado será igual a R.
 Rechazo parcial: se da cuando alguno de los comprobantes incluidos en el  request es 
rechazado. A modo  de ejemplo y con los parámetros antes descriptos,  se aprueban 
los comprobantes del  51 al 100, 101 saldrá rechazado y del 102 al  150 saldrá como no 
procesado; esto se debe a que como debe existir correlatividad numérica y de fecha, 
ante una inconsistencia los comprobantes subsiguientes también se rechazaran. Si se 
diese este caso, y para proseguir con la autorización de comprobantes se deberá 
subsanar los errores del comprobante 102 y así  enviar un nuevo  request. El campo 
Resultado será igual a P.
Operatoria con errores de comunicación: 
En el diseño del WsfeV1 se ha previsto que dada la complejidad actual de las comunicaciones- 
pueden ocurrir interrupciones en la comunicación entre el cliente y el WsfeV1 básicamente, el 
problema podría resumirse al siguiente escenario: el cliente envía una solicitud de informar 
comprobantes con CAEA y se queda esperando una respuesta que no llega, hasta que 
transcurrido algún tiempo, se produce una condición de time-out. 



# Página 160

En ese caso, el usuario no sabrá si la solicitud le llegó al WsfeV1 y fue procesado fallando la 
comunicación durante el retorno, o bien si la falla ocurrió durante el envío de la solicitud y 
simplemente WsfeV1 nunca la recibió.
En el segundo caso, con simplemente enviar la misma solicitud todo quedaría resuelto, pero en 
el primer caso, si el cliente envía nuevamente la misma solicitud para la/s misma/s factura, 
WsfeV1 devolvería un error de consecutividad puesto que en la base de datos de arca ese 
comprobante ya figura como emitido.
Para estos casos, se utiliza el  método FECompConsultar, que dado  el tipo de comprobante, 
punto  de venta y numero  de comprobante, retorna toda la información enviada en  el  
método  de registración de comprobantes con CAEA  (FECAEARegInformativo)  más  el 
resultado (A: Aprobado), tipo de emisión  (en este caso CAEA), fecha de vencimiento, fecha de 
proceso y de corresponder las observaciones realizadas al comprobante.
El WsfeV1 también ofrece un método para consultar el  último comprobante autorizado 
(FECompUltimoAutorizado) para un determinado tipo de comprobante y punto de venta.
Operatoria ante errores, Ejemplos
Se envía un request informando una Factura A.   La totalidad del comprobante es No Gravado. 
Sin errores.
REQUEST
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEARegInformativo>
         <ar:Auth>
       <ar:Token>PD…</ar:Token>
            <ar:Sign>IT…</ar:Sign>
            <ar:Cuit>23000000004</ar:Cuit>
         </ar:Auth>
         <ar:FeCAEARegInfReq>
            <ar:FeCabReq>
               <ar:CantReg>1</ar:CantReg>
               <ar:PtoVta>9800</ar:PtoVta>
               <ar:CbteTipo>1</ar:CbteTipo>
            </ar:FeCabReq>
            <ar:FeDetReq>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>33</ar:CbteDesde>
                  <ar:CbteHasta>33</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>



# Página 161

                  <ar:ImpTotal>100.00</ar:ImpTotal>
                  <ar:ImpTotConc>100.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>0.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
            </ar:FeDetReq>
         </ar:FeCAEARegInfReq>
      </ar:FECAEARegInformativo>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xmlns:xsd="http://www.w3.org/2001/XMLSchema">
   <soap:Body>
      <FECAEARegInformativoResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FECAEARegInformativoResult>
            <FeCabResp>
               <Cuit>23000000004</Cuit>
               <PtoVta>9800</PtoVta>
               <CbteTipo>1</CbteTipo>
               <FchProceso>20110306</FchProceso>
               <CantReg>1</CantReg>
               <Resultado>A</Resultado>
               <Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>33</CbteDesde>
                  <CbteHasta>33</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>A</Resultado>
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
            </FeDetResp>
         </FECAEARegInformativoResult>
      </FECAEARegInformativoResponse>
   </soap:Body>
</soap:Envelope>
Informa una Factura A.   La totalidad del comprobante es No Gravado, donde no se supera la 
totalidad de las validaciones de la CUIT emisora.



# Página 162

REQUEST
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEARegInformativo>
         <ar:Auth>
       <ar:Token>PD…</ar:Token>
            <ar:Sign>IT…</ar:Sign>
            <ar:Cuit>23000000000</ar:Cuit> CUIT no supera las 
validaciones del ticket de acceso
         </ar:Auth>
         <ar:FeCAEARegInfReq>
            <ar:FeCabReq>
               <ar:CantReg>1</ar:CantReg>
               <ar:PtoVta>9800</ar:PtoVta>
               <ar:CbteTipo>1</ar:CbteTipo>
            </ar:FeCabReq>
            <ar:FeDetReq>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>34</ar:CbteDesde>
                  <ar:CbteHasta>34</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>100.00</ar:ImpTotal>
                  <ar:ImpTotConc>100.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>0.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
            </ar:FeDetReq>
         </ar:FeCAEARegInfReq>
      </ar:FECAEARegInformativo>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xmlns:xsd="http://www.w3.org/2001/XMLSchema">
   <soap:Body>
      <FECAEARegInformativoResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FECAEARegInformativoResult>
            <Errors>   ERROR
               <Err>
                  <Code>600</Code>



# Página 163

                  <Msg>ValidacionDeToken: No apareció CUIT en lista de 
relaciones: 23000000000</Msg>
               </Err>
            </Errors>
         </FECAEARegInformativoResult>
      </FECAEARegInformativoResponse>
   </soap:Body>
</soap:Envelope>
Informa una Factura A, con error en la cabecera (FeCabReq) del comprobante, tipo de 
comprobante inválido.  Genera un Rechazo del comprobante.
REQUEST
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEARegInformativo>
         <ar:Auth>
      <ar:Token>PD..</ar:Token> 
    <ar:Sign>IT…</ar:Sign>
            <ar:Cuit>23000000004</ar:Cuit>
         </ar:Auth>
         <ar:FeCAEARegInfReq>
            <ar:FeCabReq>
               <ar:CantReg>1</ar:CantReg>
               <ar:PtoVta>9800</ar:PtoVta>
               <ar:CbteTipo>0</ar:CbteTipo>   Tipo de Comprobante 
Inválido
            </ar:FeCabReq>
            <ar:FeDetReq>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto> 
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>34</ar:CbteDesde>
                  <ar:CbteHasta>34</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>100.00</ar:ImpTotal>
                  <ar:ImpTotConc>100.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>0.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
            </ar:FeDetReq>
         </ar:FeCAEARegInfReq>



# Página 164

      </ar:FECAEARegInformativo>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xmlns:xsd="http://www.w3.org/2001/XMLSchema">
   <soap:Body>
      <FECAEARegInformativoResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FECAEARegInformativoResult>
            <FeCabResp>
               <Cuit>23000000004</Cuit>
               <PtoVta>9800</PtoVta>
               <CbteTipo>0</CbteTipo>
               <FchProceso>20110306</FchProceso>
               <CantReg>1</CantReg>
               <Resultado>R</Resultado>   Rechazo
               <Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>34</CbteDesde>
                  <CbteHasta>34</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>R</Resultado>
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
            </FeDetResp>
            <Errors>  Detalle del error
               <Err>
                  <Code>700</Code>
                  <Msg>Campo CbteTipo no se corresponde con alguno de 
los habilitados 1, 2 ,3, 6, 7 u 8.</Msg>
               </Err>
            </Errors>
         </FECAEARegInformativoResult>
      </FECAEARegInformativoResponse>
   </soap:Body>
</soap:Envelope>
Informa una Factura A, con error en el detalle (FeDetReq) del comprobante tipo de concepto 
inválido.  Genera un Rechazo del comprobante
REQUEST
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEARegInformativo>



# Página 165

         <ar:Auth>
      <ar:Token>PD..</ar:Token> 
    <ar:Sign>IT…</ar:Sign>
            <ar:Cuit>23000000004</ar:Cuit>
         </ar:Auth>
         <ar:FeCAEARegInfReq>
            <ar:FeCabReq>
               <ar:CantReg>1</ar:CantReg>
               <ar:PtoVta>9800</ar:PtoVta>
               <ar:CbteTipo>1</ar:CbteTipo>
            </ar:FeCabReq>
            <ar:FeDetReq>
               <ar:FECAEADetRequest>
                  <ar:Concepto>4</ar:Concepto>  4 valor no permitido
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>34</ar:CbteDesde>
                  <ar:CbteHasta>34</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>100.00</ar:ImpTotal>
                  <ar:ImpTotConc>100.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>0.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
            </ar:FeDetReq>
         </ar:FeCAEARegInfReq>
      </ar:FECAEARegInformativo>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xmlns:xsd="http://www.w3.org/2001/XMLSchema">
   <soap:Body>
      <FECAEARegInformativoResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FECAEARegInformativoResult>
            <FeCabResp>
               <Cuit>23000000004</Cuit>
               <PtoVta>9800</PtoVta>
               <CbteTipo>1</CbteTipo>
               <FchProceso>20110306</FchProceso>
               <CantReg>1</CantReg>
               <Resultado>R</Resultado>   Rechazo
               <Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp>



# Página 166

               <FECAEADetResponse>
                  <Concepto>4</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>34</CbteDesde>
                  <CbteHasta>34</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>R</Resultado>
                  <Observaciones>   Detalle de la causa del rechazo 
                     <Obs>
                        <Code>713</Code>
                        <Msg>El campo  Concepto  es obligatorio  y 
debe  corresponder con algún valor devuelto  por  el  método  
FEParamGetTiposConcepto</Msg>
                     </Obs>
                  </Observaciones>
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
            </FeDetResp>
         </FECAEARegInformativoResult>
      </FECAEARegInformativoResponse>
   </soap:Body>
</soap:Envelope>
Informa una Factura A, con error en el detalle del comprobante (FeDetReq) que no supera 
alguna de las validaciones No Excluyentes.  Genera una Aprobación del comprobante con 
Observaciones.
REQUEST
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEARegInformativo>
         <ar:Auth>
      <ar:Token>PD..</ar:Token> 
    <ar:Sign>IT…</ar:Sign>
            <ar:Cuit>23000000004</ar:Cuit>
         </ar:Auth>
         <ar:FeCAEARegInfReq>
            <ar:FeCabReq>
               <ar:CantReg>1</ar:CantReg>
               <ar:PtoVta>9800</ar:PtoVta>
               <ar:CbteTipo>1</ar:CbteTipo>
            </ar:FeCabReq>
            <ar:FeDetReq>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto> 
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>34</ar:CbteDesde>
                  <ar:CbteHasta>34</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>101.00</ar:ImpTotal>   Importe total 
incorrecto



# Página 167

                  <ar:ImpTotConc>100.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>0.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
            </ar:FeDetReq>
         </ar:FeCAEARegInfReq>
      </ar:FECAEARegInformativo>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xmlns:xsd="http://www.w3.org/2001/XMLSchema">
   <soap:Body>
      <FECAEARegInformativoResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FECAEARegInformativoResult>
            <FeCabResp>
               <Cuit>23000000004</Cuit>
               <PtoVta>9800</PtoVta>
               <CbteTipo>1</CbteTipo>
               <FchProceso>20110306</FchProceso>
               <CantReg>1</CantReg>
               <Resultado>A</Resultado>   Aprobado
               <Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>34</CbteDesde>
                  <CbteHasta>34</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>A</Resultado>
                  <Observaciones> Con Observaciones
                     <Obs>
                        <Code>724</Code>
                        <Msg>El campo  'Importe Total' ImpTotal, debe 
ser igual  a la  suma de ImpTotConc + ImpNeto + ImpOpEx + ImpTrib + 
ImpIVA.</Msg>
                     </Obs>
                  </Observaciones>
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
            </FeDetResp>
         </FECAEARegInformativoResult>
      </FECAEARegInformativoResponse>
   </soap:Body>



# Página 168

</soap:Envelope>
Se envía un Request con tres Facturas A, que superan la totalidad de las validaciones. Genera 
una aprobación total de la solicitud.
REQUEST
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEARegInformativo>
         <ar:Auth>
      <ar:Token>PD…</ar:Token>
            <ar:Sign>jd..</ar:Sign>
            <ar:Cuit>23000000004</ar:Cuit>
         </ar:Auth>
         <ar:FeCAEARegInfReq>
            <ar:FeCabReq>
               <ar:CantReg>3</ar:CantReg>
               <ar:PtoVta>9800</ar:PtoVta>
               <ar:CbteTipo>1</ar:CbteTipo>
            </ar:FeCabReq>
            <ar:FeDetReq>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>35</ar:CbteDesde>
                  <ar:CbteHasta>35</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>200.00</ar:ImpTotal>
                  <ar:ImpTotConc>100.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>100.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>36</ar:CbteDesde>
                  <ar:CbteHasta>36</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>101.00</ar:ImpTotal>
                  <ar:ImpTotConc>100.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>0.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>



# Página 169

                  <ar:ImpTrib>1</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:Tributos>
                     <ar:Tributo>
                        <ar:Id>99</ar:Id>
                        <ar:Desc>Otro tributo</ar:Desc>
                        <ar:BaseImp>100</ar:BaseImp>
                        <ar:Alic>1</ar:Alic>
                        <ar:Importe>1</ar:Importe>
                     </ar:Tributo>
                  </ar:Tributos>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>37</ar:CbteDesde>
                  <ar:CbteHasta>37</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>100.00</ar:ImpTotal>
                  <ar:ImpTotConc>0.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>100.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
            </ar:FeDetReq>
         </ar:FeCAEARegInfReq>
      </ar:FECAEARegInformativo>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xmlns:xsd="http://www.w3.org/2001/XMLSchema">
   <soap:Body>
      <FECAEARegInformativoResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FECAEARegInformativoResult>
            <FeCabResp>
               <Cuit>23000000004</Cuit>
               <PtoVta>9800</PtoVta>
               <CbteTipo>1</CbteTipo>
               <FchProceso>20110308</FchProceso>
               <CantReg>3</CantReg>
               <Resultado>A</Resultado>   Aprobación total del envío
               <Reproceso>N</Reproceso>
            </FeCabResp>



# Página 170

            <FeDetResp>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>35</CbteDesde>
                  <CbteHasta>35</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>A</Resultado>  Aprobación del comprob.
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>36</CbteDesde>
                  <CbteHasta>36</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>A</Resultado>  Aprobación del comprob.
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>36</CbteDesde>
                  <CbteHasta>36</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>A</Resultado>  Aprobación del comprob.
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
            </FeDetResp>
         </FECAEARegInformativoResult>
      </FECAEARegInformativoResponse>
   </soap:Body>
</soap:Envelope>
Se envía un Request con tres Facturas A (número 38, 39 y 40), donde la número  38 supera 
todas las validaciones excluyentes y la número 39 no supera una de las validaciones 
excluyentes. Genera una aprobación parcial de la solicitud, el comprobante 38 es aprobado, el 
39 rechazado y el 40 no es procesado generando su rechazo.
REQUEST
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEARegInformativo>
         <ar:Auth>
      <ar:Token>PD…</ar:Token>
            <ar:Sign>jd..</ar:Sign>
            <ar:Cuit>23000000004</ar:Cuit>
         </ar:Auth>



# Página 171

         <ar:FeCAEARegInfReq>
            <ar:FeCabReq>
               <ar:CantReg>3</ar:CantReg>
               <ar:PtoVta>9800</ar:PtoVta>
               <ar:CbteTipo>1</ar:CbteTipo>
            </ar:FeCabReq>
            <ar:FeDetReq>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>38</ar:CbteDesde>
                  <ar:CbteHasta>38</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>200.00</ar:ImpTotal>
                  <ar:ImpTotConc>100.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>100.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>39</ar:CbteDesde>
                  <ar:CbteHasta>39</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>101.00</ar:ImpTotal>
                  <ar:ImpTotConc>100.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>0.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>
                  <ar:ImpTrib>1</ar:ImpTrib>   Se informa ImpTrib 
mayor a cero y no se informa el detalle <Tributos>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>40</ar:CbteDesde>
                  <ar:CbteHasta>40</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>100.00</ar:ImpTotal>
                  <ar:ImpTotConc>0.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>100.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>



# Página 172

                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
            </ar:FeDetReq>
         </ar:FeCAEARegInfReq>
      </ar:FECAEARegInformativo>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xmlns:xsd="http://www.w3.org/2001/XMLSchema">
   <soap:Body>
      <FECAEARegInformativoResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FECAEARegInformativoResult>
            <FeCabResp>
               <Cuit>23000000004</Cuit>
               <PtoVta>9800</PtoVta>
               <CbteTipo>1</CbteTipo>
               <FchProceso>20110308</FchProceso>
               <CantReg>3</CantReg>
               <Resultado>P</Resultado>   Aprobación Parcial de la 
            solicitud
               <Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>38</CbteDesde>
                  <CbteHasta>38</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>A</Resultado>   Aprobación del 
Comprobante
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>39</CbteDesde>
                  <CbteHasta>39</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>R</Resultado>  Rechazo del 
Comprobante
                  <Observaciones>   Motivo del rechazo
                     <Obs>
                        <Code>900</Code>



# Página 173

                        <Msg>Si ImpTrib es mayor a 0 el objeto 
Tributos y Tributo son obligatorios.</Msg>
                     </Obs>
                  </Observaciones>
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>40</CbteDesde>
                  <CbteHasta>40</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>R</Resultado>   Rechazo del comprobante 
no fue procesado por haber sido rechazado el comprobante anterior 
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
            </FeDetResp>
         </FECAEARegInformativoResult>
      </FECAEARegInformativoResponse>
   </soap:Body>
</soap:Envelope>
Se envía un Request con tres Facturas A (número 39, 40 y 41), donde la información enviada 
en la cabecera del comprobante tiene alguna inconsistencia (se informa que el Request 
contiene dos comprobantes y se envían tres) entonces se genera un rechazo total de la 
solicitud.
REQUEST
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEARegInformativo>
         <ar:Auth>
      <ar:Token>PD…</ar:Token>
            <ar:Sign>jd..</ar:Sign>
            <ar:Cuit>23000000004</ar:Cuit>
         </ar:Auth>
         <ar:FeCAEARegInfReq>
            <ar:FeCabReq>
               <ar:CantReg>2</ar:CantReg>
               <ar:PtoVta>9800</ar:PtoVta>
               <ar:CbteTipo>1</ar:CbteTipo>
            </ar:FeCabReq>
            <ar:FeDetReq>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>39</ar:CbteDesde>
                  <ar:CbteHasta>39</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>



# Página 174

                  <ar:ImpTotal>200.00</ar:ImpTotal>
                  <ar:ImpTotConc>100.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>100.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>40</ar:CbteDesde>
                  <ar:CbteHasta>40</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>100.00</ar:ImpTotal>
                  <ar:ImpTotConc>100.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>0.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>41</ar:CbteDesde>
                  <ar:CbteHasta>41</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>100.00</ar:ImpTotal>
                  <ar:ImpTotConc>0.00</ar:ImpTotConc>
                  <ar:ImpNeto>0</ar:ImpNeto>
                  <ar:ImpOpEx>100.00</ar:ImpOpEx>
                  <ar:ImpIva>0</ar:ImpIva>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
            </ar:FeDetReq>
         </ar:FeCAEARegInfReq>
      </ar:FECAEARegInformativo>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE



# Página 175

<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xmlns:xsd="http://www.w3.org/2001/XMLSchema">
   <soap:Body>
      <FECAEARegInformativoResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FECAEARegInformativoResult>
            <FeCabResp>
               <Cuit>23000000004</Cuit>
               <PtoVta>9800</PtoVta>
               <CbteTipo>1</CbteTipo>
               <FchProceso>20110308</FchProceso>
               <CantReg>2</CantReg>
               <Resultado>R</Resultado>  Rechazo total del envío
               <Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>39</CbteDesde>
                  <CbteHasta>39</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>R</Resultado>
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>40</CbteDesde>
                  <CbteHasta>40</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>R</Resultado>
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>41</CbteDesde>
                  <CbteHasta>41</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>R</Resultado>
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
            </FeDetResp>
            <Errors>
               <Err>   Motivo del Rechazo
                  <Code>10002</Code>
                  <Msg>Campo CantReg debe ser igual a lo informado en 
detalle. Informado: 2, Enviado:3</Msg>
               </Err>
            </Errors>
         </FECAEARegInformativoResult>
      </FECAEARegInformativoResponse>



# Página 176

   </soap:Body>
</soap:Envelope>
Se envía un Request con una Facturas B con Importe Gravado y alícuota de IVA al 21%, supera 
la totalidad de las validaciones.  El comprobante es aprobado.
REQUEST
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEARegInformativo>
         <ar:Auth>
      <ar:Token>PD…</ar:Token>
            <ar:Sign>jd..</ar:Sign>
            <ar:Cuit>23000000004</ar:Cuit>
         </ar:Auth>
         <ar:FeCAEARegInfReq>
            <ar:FeCabReq>
               <ar:CantReg>1</ar:CantReg>
               <ar:PtoVta>9800</ar:PtoVta>
               <ar:CbteTipo>6</ar:CbteTipo>
            </ar:FeCabReq>
            <ar:FeDetReq>
               <ar:FECAEADetRequest>
                  <ar:Concepto>2</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>45</ar:CbteDesde>
                  <ar:CbteHasta>45</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>121.00</ar:ImpTotal>
                  <ar:ImpTotConc>0.00</ar:ImpTotConc>
                  <ar:ImpNeto>100</ar:ImpNeto> Imp Neto Gravado
                  <ar:ImpOpEx>0.00</ar:ImpOpEx>
                  <ar:ImpIVA>21</ar:ImpIVA>   Importe IVA liquidado
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:FchServDesde>20110101</ar:FchServDesde>
                  <ar:FchServHasta>20110102</ar:FchServHasta>
                  <ar:FchVtoPago>20110220</ar:FchVtoPago>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:Iva>
                     <ar:AlicIva>
                        <ar:Id>5</ar:Id> Alícuota de IVA 21%
                        <ar:BaseImp>100</ar:BaseImp> Base Imponible 
para la Alícuota indicada en Id
                        <ar:Importe>21</ar:Importe> Imp IVA liquidado
                     </ar:AlicIva>
                  </ar:Iva>



# Página 177

                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
            </ar:FeDetReq>
         </ar:FeCAEARegInfReq>
      </ar:FECAEARegInformativo>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xmlns:xsd="http://www.w3.org/2001/XMLSchema">
   <soap:Body>
      <FECAEARegInformativoResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FECAEARegInformativoResult>
            <FeCabResp>
               <Cuit>23000000004</Cuit>
               <PtoVta>9800</PtoVta>
               <CbteTipo>6</CbteTipo>
               <FchProceso>20110314</FchProceso>
               <CantReg>1</CantReg>
               <Resultado>A</Resultado>
               <Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp>
               <FECAEADetResponse>
                  <Concepto>2</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>45</CbteDesde>
                  <CbteHasta>45</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>A</Resultado>
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
            </FeDetResp>
         </FECAEARegInformativoResult>
      </FECAEARegInformativoResponse>
   </soap:Body>
</soap:Envelope>
Se envía un Request con una Facturas A con Importe Gravado, alícuota de IVA al 21%,  27% y al 
0%,  Importes Exentos y No Gravados y con importes de Tributos (IIBB), supera la totalidad de 
las validaciones.  El comprobante es aprobado.
REQUEST
<soapenv:Envelope <soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">



# Página 178

   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEARegInformativo>
         <ar:Auth>
      <ar:Token>PD…</ar:Token>
            <ar:Sign>jd..</ar:Sign>
            <ar:Cuit>23000000004</ar:Cuit>
         </ar:Auth>
         <ar:FeCAEARegInfReq>
            <ar:FeCabReq>
               <ar:CantReg>1</ar:CantReg>
               <ar:PtoVta>9800</ar:PtoVta>
               <ar:CbteTipo>1</ar:CbteTipo>
            </ar:FeCabReq>
            <ar:FeDetReq>
               <ar:FECAEADetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>30000000007</ar:DocNro>
                  <ar:CbteDesde>40</ar:CbteDesde>
                  <ar:CbteHasta>40</ar:CbteHasta>
                  <ar:CbteFch>20110211</ar:CbteFch>
                  <ar:ImpTotal>549.00</ar:ImpTotal>
                  <ar:ImpTotConc>100.00</ar:ImpTotConc>  No Gravado
                  <ar:ImpNeto>300</ar:ImpNeto>  Gravado.  Igual a la 
  sumatoria de BaseImp del Array de Iva.
                  <ar:ImpOpEx>100.00</ar:ImpOpEx>  Imp Exento
                  <ar:ImpIVA>48</ar:ImpIVA>  Importe total de IVA 
liquidado. Es igual a la sumatoria de Importe del Array de Iva.
                  <ar:ImpTrib>1</ar:ImpTrib>  Importe total de 
tributos. Es igual a la sumatoria de Importe del Array de Tributos.
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVARecep-
torId>
                  <ar:Tributos>  Detalle de Tributos
                     <ar:Tributo>
                        <ar:Id>2</ar:Id>
                        <ar:Desc>IIBB Pcia Bs AS</ar:Desc>
                        <ar:BaseImp>100</ar:BaseImp>
                        <ar:Alic>1</ar:Alic>
                        <ar:Importe>1</ar:Importe>
                     </ar:Tributo>
                  </ar:Tributos>
  <ar:Iva>  Detalle IVA Liquidado
                     <ar:AlicIva>
                        <ar:Id>5</ar:Id>  21% IVA
                        <ar:BaseImp>100</ar:BaseImp> Base Imponible 
para la Alícuota indicada en Id
                        <ar:Importe>21</ar:Importe> Imp IVA liquidado
según Alícuota y Base Imponible.
                     </ar:AlicIva>
     <ar:AlicIva>
                        <ar:Id>3</ar:Id>  0% IVA
                        <ar:BaseImp>100</ar:BaseImp>Base Imponible 
para la alícuota indicada en Id



# Página 179

                        <ar:Importe>0</ar:Importe> Imp IVA liquidado
según Alícuota y Base Imponible.
                     </ar:AlicIva>
                     <ar:AlicIva>
                        <ar:Id>6</ar:Id>  27% IVA
                        <ar:BaseImp>100</ar:BaseImp> Base Imponible 
para la Alícuota indicada en Id
                        <ar:Importe>27</ar:Importe> Imp IVA liquidado
según Alícuota y Base Imponible.
                     </ar:AlicIva>
                  </ar:Iva>
                  <ar:CAEA>21064126523746</ar:CAEA>
               </ar:FECAEADetRequest>
            </ar:FeDetReq>
         </ar:FeCAEARegInfReq>
      </ar:FECAEARegInformativo>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
xmlns:xsd="http://www.w3.org/2001/XMLSchema">
   <soap:Body>
      <FECAEARegInformativoResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FECAEARegInformativoResult>
            <FeCabResp>
               <Cuit>23000000004</Cuit>
               <PtoVta>9800</PtoVta>
               <CbteTipo>6</CbteTipo>
               <FchProceso>20110314</FchProceso>
               <CantReg>1</CantReg>
               <Resultado>A</Resultado>
               <Reproceso>N</Reproceso>
            </FeCabResp>
            <FeDetResp>
               <FECAEADetResponse>
                  <Concepto>1</Concepto>
                  <DocTipo>80</DocTipo>
                  <DocNro>30000000007</DocNro>
                  <CbteDesde>40</CbteDesde>
                  <CbteHasta>40</CbteHasta>
                  <CbteFch>20110211</CbteFch>
                  <Resultado>A</Resultado>
                  <CAEA>21064126523746</CAEA>
               </FECAEADetResponse>
            </FeDetResp>
         </FECAEARegInformativoResult>
      </FECAEARegInformativoResponse>
   </soap:Body>
</soap:Envelope>



# Página 180

Método  para consultar CAEA sin movimiento (FECAEASinMovimientoConsultar)
Esta operación permite consultar mediante un CAEA, cuáles fueron los puntos de venta que 
fueron notificados como sin movimiento. El cliente envía el requerimiento, el cual es atendido 
por el WS, superadas las validaciones de seguridad se informa el CAEA, puntos de venta 
identificados como sin movimientos y fecha de proceso. En caso de informar el punto de 
venta, se informan los datos vinculados a ese punto de venta en particular.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx  ?op=   
FECAEASinMovimientoConsultar
Mensaje de solicitud
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECAEASinMovimientoConsultar>
       <ar:Auth>
            <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
         <ar:CAEA>string</ar:CAEA>
         <ar:PtoVta>int</ar:PtoVta>
      </ar:FECAEASinMovimientoConsultar>
   </soapenv:Body>
</soapenv:Envelope>
dónde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign y Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Campo Detalle Obligatorio
CAEA CAEA otorgado, e identificado como “Sin Movimientos”  S



# Página 181

para determinados puntos de venta.
PtoVta Punto de venta vinculado al CAEA informado. S
Mensaje de respuesta
Retorna los puntos de venta vinculados al CAEA ingresado por parámetro donde los mismos 
estén registrados como sin movimientos.
<soap12:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-
envelope" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soap12:Header/>
   <soap12:Body>
      <FECAEASinMovimientoConsultarResponse>
         <FECAEASinMovimientoConsultarResult>
            <ResultGet>
               <FECAEASinMov>
                  <CAEA>string</CAEA>
                  <FchProceso>string</FchProceso>
                  <PtoVta>int</PtoVta>
               </FECAEASinMov>
            </ResultGet>
            <Errors>
               <Err>
                  <Code>int</Code>
                  <Msg>string</Msg>
               </Err>
            </Errors>
            <Events>
               <Evt>
                  <Code>int</Code>
                  <Msg>string</Msg               </Evt>
            </Events>
         </FECAEASinMovimientoConsultarResult>
      </FECAEASinMovimientoConsultarResponse>
   </soapenv:Body>
</soapenv:Envelope>
dónde:
FECAEASinMovimientoResult
Campo Detalle Obligatorio
ResultGet
Nodo contenedor del array de elementos correspondientes 
a él o los puntos de venta identificados como sin 
movimientos para el CAEA identificado.
S
Errors Información de errores detectados N
Events Información de eventos N



# Página 182

ResultGet: contiene la información de los puntos de venta informados 
Campo Tipo Detalle Obligatorio
CAEA String (14) Código de Autorización electrónico anticipado S
FchProceso String (8) Fecha de en que se informó cómo sin 
movimiento al CAEA Pto Vta indicados. S
PtoVta Int (5) Punto de venta vinculado al CAEA 
informado. S
Validaciones y errores 
Controles aplicados 
Campo / Grupo Código de 
error Validación
CAEA 10100 No ingreso el CAEA o el formato es inválido.
PtoVta 10101 No ingreso el Punto de Venta o el formato es inválido.
CAEA 10102 El CAEA informado no se encuentra registrado en las 
bases de la Administración como sin movimientos.
CAEA / PtoVta 10105 El punto de venta ingresado registra comprobantes 
informados
Método  para consultar Comprobantes Emitidos y su código (FECompConsultar)
Esta operación permite consultar mediante tipo, numero de comprobante y punto de venta los 
datos de un comprobante ya emitido. Dentro de los datos del comprobante resultante se 
obtiene el tipo de emisión utilizado para generar el código de autorización.
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx  ?op=   FECompConsultar  



# Página 183

Mensaje de solicitud
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECompConsultar>
      <ar:Auth>
            <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
         <ar:FeCompConsReq>
            <ar:CbteTipo>int</ar:CbteTipo>
            <ar:CbteNro>long</ar:CbteNro>
            <ar:PtoVta>int</ar:PtoVta>
         </ar:FeCompConsReq>
      </ar:FECompConsultar>
   </soapenv:Body>
</soapenv:Envelope>
donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign y Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Campo Detalle Obligatorio
FeCompConsReq Información del comprobante que se desea consultar. S
Campo Detalle Obligatorio
CbteTipo Tipo de Comprobante S
CbteNro Número de comprobante S
PtoVta Punto de venta S
Mensaje de respuesta
Retorna los datos del Comprobante coincidente con los parámetros ingresados.



# Página 184

<soap12:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-
envelope" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soap12:Header/>
   <soap12:Body>
      <FECompConsultarResponse>
         <FECompConsultarResult>
            <ResultGet>
               <Concepto>int</Concepto>
               <DocTipo>int</DocTipo>
               <DocNro>long</DocNro>
               <CbteDesde>long</CbteDesde>
               <CbteHasta>long</CbteHasta>
               <CbteFch>string</CbteFch>
               <ImpTotal>double</ImpTotal>
               <ImpTotConc>double</ImpTotConc>
               <ImpNeto>double</ImpNeto>
               <ImpOpEx>double</ImpOpEx>
               <ImpTrib>double</ImpTrib>
               <ImpIVA>double</ImpIVA>
               <FchServDesde>string</FchServDesde>
               <FchServHasta>string</FchServHasta>
               <FchVtoPago>string</FchVtoPago>
               <MonId>string</MonId>
               <MonCotiz>double</MonCotiz>
               <CbtesAsoc>
                  <CbteAsoc>
                     <Tipo>int</Tipo>
                     <PtoVta>int</PtoVta>
                     <Nro>long</Nro>
                  </CbteAsoc>
               </CbtesAsoc>
               <Tributos>
                  <Tributo>
                     <Id>int</Id>
                     <Desc>string</Desc>
                     <BaseImp>double</BaseImp>
                     <Alic>double</Alic>
                     <Importe>double</Importe>
                  </Tributo>
               </Tributos>
               <Iva>
                  <AlicIva>
                     <Id>int</Id>
                     <BaseImp>double</BaseImp>
                     <Importe>double</Importe>
                  </AlicIva>
               </Iva>
               <Opcionales>
                  <Opcional>
                     <Id>string</Id>
                     <Valor>string</Valor>
                  </Opcional>
               </Opcionales>
               <Compradores>
                  <Comprador>
                    <DocTipo>int</DocTipo>
                    <DocNro>long</DocNro>



# Página 185

                    <Porcentaje>double</Porcentaje>
                  </Comprador>
               </Compradores>
               <ar:PeriodoAsoc>
                  <ar:FchDesde>string</ar:FchDesde>
                  <ar:FchHasta>string</ar:FchHasta>
               </ar:PeriodoAsoc>
               <Resultado>string</Resultado>
               <CodAutorizacion>string</CodAutorizacion>
               <EmisionTipo>string</EmisionTipo>
               <FchVto>string</FchVto>
               <FchProceso>string</FchProceso>
               <Observaciones>
                  <Obs>
                     <Code>int</Code>
                     <Msg>string</Msg>
                  </Obs>
               </Observaciones>
               <PtoVta>int</PtoVta>
               <CbteTipo>int</CbteTipo>
            </ResultGet>
            <Errors>
               <Err>
                  <Code>int</Code>
                  <Msg>string</Msg>
               </Err>
            </Errors>
            <Events>
               <Evt>
                  <Code>int</Code>
                  <Msg>string</Msg>
               </Evt>
            </Events>
         </FECompConsultarResult>
      </FECompConsultarResponse>
   </soapenv:Body>
</soapenv:Envelope>
dónde:
Campo Detalle Obligatorio
FECompConsultarResult Nodo contenedor correspondiente a él comprobante 
solicitado. Contiene los datos ResultGet, Errors y 
Events
S
Errors Información de errores detectados N
Events Información de eventos N
ResultGet: El objeto resultante informante del resultado del proceso contiene los campos 
identificados como valores de entrada FECAEDetRequest (request) en el método 
FECAESolicitar + los siguientes atributos.



# Página 186

Campo Detalle Obligatorio
Resultado Resultado del procesamiento del comprobante S
CodAutorizacion Código de Autorización S
EmisionTipo Tipo de emisión, si corresponde a CAE o CAEA S
FchVto Vencimiento del código de autorización.  Si tipo de emisión es 
igual a CAE esta es la fecha de vencimiento obtenida cuando 
se autorizó el comprobante.  Si tipo de emisión es igual a 
CAEA esta es la fecha de “vigencia hasta” del CAEA obtenida 
cuando gestionó el CAEA.
S
FchProceso Fecha de procesamiento del comprobante S
Observaciones Observaciones identificadas al momento de generar el 
comprobante.
N
PtoVta Punto de venta S
CbteTipo Tipo de Comprobante S
Validaciones y errores 
Controles aplicados 
Campo / Grupo Código de error Validación
PtoVta 10200 No ingreso el Punto de Venta o el formato es 
inválido.
CbteTipo 10201 No ingreso el Tipo de Comprobante, o el tipo 
de comprobante es inválido.
PtoVta 10104 El punto de venta ingresado no se encuentra 
registrado.
CbteNro 10202 No ingreso el número de comprobante o el 
formato es inválido.



# Página 187

Ejemplo
REQUEST
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FECompConsultar>
      <ar:Auth>
            <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>33693450239</ar:Cuit>
         </ar:Auth>
         <ar:FeCompConsReq>
            <ar:CbteTipo>1</ar:CbteTipo>
            <ar:CbteNro>1</ar:CbteNro>
            <ar:PtoVta>12</ar:PtoVta>
         </ar:FeCompConsReq>
      </ar:FECompConsultar>
   </soapenv:Body>
</soapenv:Envelope>
RESPONSE
<soap12:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soap12:Header/>
   <soap12:Body>
      <FECompConsultarResponse>
         <FECompConsultarResult>
            <ResultGet>
               <Concepto>1</Concepto>
               <DocTipo>80</DocTipo>
               <DocNro>20111111112</DocNro>
               <CbteDesde>1</CbteDesde>
               <CbteHasta>1</CbteHasta>
               <CbteFch>20100903</CbteFch>
               <ImpTotal>184.05</ImpTotal>
               <ImpTotConc>0</ImpTotConc>
               <ImpNeto>150</ImpNeto>
               <ImpOpEx>0</ImpOpEx>
               <ImpTrib>7.8</ImpTrib>
               <ImpIVA>26.25</ImpIVA>
               <FchServDesde></FchServDesde>
               <FchServHasta></FchServHasta>
               <FchVtoPago></FchVtoPago>
               <MonId>PES</MonId>
               <MonCotiz>1</MonCotiz>
               <Tributos>
                  <Tributo>
                     <Id>99</Id>
                     <Desc>Impuesto Municipal Matanza</Desc>
                     <BaseImp>150</BaseImp>
                     <Alic>5.2</Alic>



# Página 188

                     <Importe>7.8</Importe>
                  </Tributo>
               </Tributos>
               <Iva>
                  <AlicIva>
                     <Id>5</Id>
                     <BaseImp>100</BaseImp>
                     <Importe>21</Importe>
                  </AlicIva>
                  <AlicIva>
                     <Id>4</Id>
                     <BaseImp>50</BaseImp>
                     <Importe>5.25</Importe>
                  </AlicIva>
</Iva>
               <Resultado>A</Resultado>
               <CodAutorizacion>41124578989845</CodAutorizacion>
               <EmisionTipo>CAE</EmisionTipo>
               <FchVto>20100913</FchVto>
               <FchProceso>20100902</FchProceso>
               <PtoVta>12</PtoVta>
               <CbteTipo>1</CbteTipo>
            </ResultGet>
         </FECompConsultarResult>
      </FECompConsultarResponse>
   </soapenv:Body>
</soapenv:Envelope>
Método  para consultar valores referenciales de códigos de países 
(FEParamGetTiposPaises)
Esta operación permite consultar los códigos de países y descripción de los mismos.
 
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx  ?op=   
FEParamGetTiposPaises
Mensaje de solicitud
Recibe las credenciales de autenticación y la cuit del usuario representado.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEParamGetTiposPaises>
         <ar:Auth>
            <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>



# Página 189

      </ar:FEParamGetTiposPaises>
   </soapenv:Body>
</soapenv:Envelope>
donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign , Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S
Mensaje de respuesta
<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-
instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
     <FEParamGetTiposPaisesResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FEParamGetTiposPaisesResult>
            <ResultGet>
               <PaisTipo>
                  <Id>int</Id>
                  <Desc>string</Desc>
               </PaisTipo>
               <PaisTipo>
                  <Id>int</Id>
                  <Desc>string</Desc>
               </PaisTipo>
               <PaisTipo>
                  <Id>int</Id>
                  <Desc>string</Desc>
               </PaisTipo>
               <PaisTipo>
                  <Id>int</Id>
                  <Desc>string</Desc>
               </PaisTipo>
           </ResultGet>
         </FEParamGetTiposPaisesResult>
      </FEParamGetTiposPaisesResponse>
   </soap:Body>
</soap:Envelope>
dónde: 
FEParamGetTiposPaisesResult:
Campo Detalle Obligatorio



# Página 190

ResultGet Información sobre los tipos de países aceptados. N
Errors Información de errores detectados N
Events Información de eventos N
ResultGet:
Campo Tipo Detalle Obligatorio
Id Int (3) Código  de país S
Desc String (250) Descripción S
Método  para consultar las actividades  (FEParamGetActividades)
Esta operación permite consultar los códigos de actividades, sus descripciones y el orden (si es 
actividad primaria, secundaria, etc)
 
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx  ?op=   
FEParamGetActividades
Mensaje de solicitud
Recibe las credenciales de autenticación y la cuit del usuario representado.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEParamGetActividades>
         <ar:Auth>
            <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
      </ar: FEParamGetActividades>
   </soapenv:Body>
</soapenv:Envelope>
Dónde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign , Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S



# Página 191

Cuit Cuit contribuyente (representado o Emisora) S
Mensaje de respuesta
<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-
instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
     < FEParamGetActividadesResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FEParamGetActividadesResult>
            <ResultGet>
               < ActividadesTipo>
                  <Id>long</Id>
                  <Orden>short</Orden>
                  <Desc>string</Desc>
               </ ActividadesTipo>
               < ActividadesTipo>
                  <Id>long</Id>
                  <Orden>short</Orden>
                  <Desc>string</Desc>
               </ ActividadesTipo>
               < ActividadesTipo>
                  <Id>long</Id>
                  <Orden>short</Orden>
                  <Desc>string</Desc>
               </ ActividadesTipo>
               < ActividadesTipo>
                  <Id>long</Id>
                  <Orden>short</Orden>
                  <Desc>string</Desc>
               </ ActividadesTipo>
           </ResultGet>
         </FEParamGetActividadesResult>
      </FEParamGetActividadesResponse>
   </soap:Body>
</soap:Envelope>
Dónde: 
FEParamGetTiposPaisesResult:
Campo Detalle Obligatorio
ResultGet Información sobre los tipos de actividades que el emisor 
tiene habilitadas. N
Errors Información de errores detectados N
Events Información de eventos N
ResultGet:
Campo Tipo Detalle Obligatorio



# Página 192

Id Long Identificador de Actividad S
Orden Short(3) Orden de la Actividad S
Desc String (180) Descripción de la Actividad S
Método  para consultar valores referenciales de los identificadores de la condición frente 
al IVA del receptor (FEParamGetCondicionIvaReceptor)
Esta operación permite consultar los identificadores de la condicion frente al IVA del receptor, 
su descripción y a la clase de comprobante que corresponde.
 
Dirección URL (Homologación)
Este servicio se llama desde: 
https://wswhomo.afip.gov.ar/wsfev1/service.asmx  ?  
op=  FEParamGetCondicionIvaReceptor  
Mensaje de solicitud
Recibe las credenciales de autenticación y la cuit del usuario representado.
<soapenv:Envelope 
xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
xmlns:ar="http://ar.gov.afip.dif.FEV1/">
   <soapenv:Header/>
   <soapenv:Body>
      <ar:FEParamGetCondicionFrenteIvaReceptor>
         <ar:Auth>
            <ar:Token>string</ar:Token>
            <ar:Sign>string</ar:Sign>
            <ar:Cuit>long</ar:Cuit>
         </ar:Auth>
         <ar:ClaseCmp>string</ar:ClaseCmp>
      </ar:FEParamGetCondicionFrenteIvaReceptor>
   </soapenv:Body>
</soapenv:Envelope>
donde:
Campo Detalle Obligatorio
Auth Información de la autenticación. Contiene los datos de 
Token, Sign , Cuit S
Token Token devuelto  por  el  WSAA S
Sign Sign devuelto  por  el  WSAA S
Cuit Cuit contribuyente (representado o Emisora) S



# Página 193

ClaseCmp
Clase de comprobante. Valores posibles A, M, B, C o BU 
(Bienes Usados). En caso de no informar el dicho campo, 
el método lista todas las combinaciones posibles de todas 
las clases de comprobantes.
N
Mensaje de respuesta
<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-
instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
     <FEParamGetCondicionIvaReceptorResponse 
xmlns="http://ar.gov.afip.dif.FEV1/">
         <FEParamGetCondicionIvaReceptorResult>
            <ResultGet>
               <CondicionIvaReceptor>
                  <Id>int</Id>
                  <Desc>string</Desc>
                  <Cmp_Clase>string</Cmp_Clase>
               </CondicionIvaReceptor>
               <CondicionIvaReceptor>
                  <Id>int</Id>
                  <Desc>string</Desc>
                  <Cmp_Clase>string</Cmp_Clase>
               </CondicionIvaReceptor>
               <CondicionIvaReceptor>
                  <Id>int</Id>
                  <Desc>string</Desc>
                  <Cmp_Clase>string</Cmp_Clase>
               </CondicionIvaReceptor>
               <CondicionIvaReceptor>
                  <Id>int</Id>
                  <Desc>string</Desc>
                  <Cmp_Clase>string</Cmp_Clase>
               </CondicionIvaReceptor>
           </ResultGet>
         </FEParamGetCondicionIvaReceptorResult>
      </FEParamGetCondicionIvaReceptorResponse>
   </soap:Body>
</soap:Envelope>
Dónde: 
FEParamGetCondicionIvaReceptorResult:
Campo Detalle Obligatorio
CondicionIvaRece
ptor
Información sobre la combinación de la condición de iva de 
receptor y la clase de comprobante permitidas. N
Errors Información de errores detectados N
Events Información de eventos N



# Página 194

CondicionIvaReceptor:
Campo Tipo Detalle Obligatorio
Id Int (3) Identificador de la condicion de IVA del 
receptor S
Desc String (250) Descripción S
Cmp_Clase String (5) Clase de comprobante S
Validaciones y errores 
Validaciones que se aplican sobre el objeto <FEParamGetCondicionIvaReceptor>
Campo/Grupo Código 
de error Descripción de la validación
Cmp_Clase
10244
El valor del c ampo <Cmp_Clase> ingresado para la clase de compro -
bante no es valido. La clase de Comprobante es opcional, de ingresar  
un valor solo puede ser A, B, C, M o 49 (Bienes Usados),
Margen de error mediante (Error Absoluto y Error Relativo)
Error Absoluto eabs: Es la diferencia entre el valor medido (calculado) y el valor real
Error Relativo erel : Es el cociente entre el valor error absoluto y el valor real.
En ambos casos se tomará el valor absoluto, es decir el signo resultante de la opera-
ción no se considerará.
El criterio de redondeo que utilizamos en este servicio es Round Half Even.



# Página 195

Condición Frente al IVA del receptor
CONDICIÓN FRENTE AL IVA
Código Descripción
COMPROBANTE
CLASE
49 – Comprob. 
de Compra de 
Bienes Usados
A/M B C 49
1 IVA Responsable Inscripto X X
4 IVA Sujeto Exento X X
5 Consumidor Final X X X
6 Responsable Monotributo X X
7 Sujeto No Categorizado X X
8 Proveedor del Exterior X X
9 Cliente del Exterior X X
10 IVA Liberado – Ley N° 19.640 X X
13 Monotributista Social X X
15 IVA No Alcanzado X X
16
Monotributo Trabajador 
Independiente Promovido X X
