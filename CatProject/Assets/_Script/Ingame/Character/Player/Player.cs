﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

enum ANIMATION_STATE { Idle = 0, Walk, Jump, Fall, Attack, TakeDamage, Die }
/// <summary>
/// NOTE : 플레이어캐릭터 공격 점프 이동
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class Player : MonoBehaviour
{
    /// <summary>
    /// CHARACTER STATE 
    /// NOTE : 애니매이션 파라미터 상태 설정 (속성은 animation 속도 설정)
    /// WALK : 이동 중일 경우 이동 하는 속도에 맞춰서 발걸음을 느려지게 하기 위함
    /// ATTACK : 공격 속도 설정
    /// TODO : 위 2개 말고는 아직은 필요성을 느끼지 못함
    private ANIMATION_STATE InstanceState;
    private ANIMATION_STATE CurrentPlayerAnimState
    {
        get { return InstanceState; }
        set
        {
            InstanceState = value;
            switch (InstanceState)
            {
                case ANIMATION_STATE.Walk:
                    anim.speed = Mathf.Abs(currentMoveSpeed * 0.1f);
                    break;
                case ANIMATION_STATE.Attack:
                    anim.speed = pDATA.attackAnimSpeed;
                    break;
                default:
                    anim.speed = 1;
                    break;
            }
        }
    }

    [Header("PLAYER DATA SET"), SerializeField]
    private PlayerData pDATA;

    private Rigidbody2D rb2D;
    private Animator anim;
    private SpriteRenderer characterSprite;
    //UI
    private Slider HPSlider;
    private Text HPText;
    private Slider TPSlider;
    private Text TPText;

    [HideInInspector]
    public bool jumpButtonPress, attackButtonPress = false;
    private bool stopOn, attackOn;

    private bool isRunningJumpCoroutine = false;
    private bool isRunningAttackCoroutine = false;
    private bool isRunningStopCoroutine = false;
    private bool isRunningRecoverTPCoroutine = false;
    
    private bool isAlive = true;
    private bool isInvincible = false;
    //HP
    private int instanceHP;
    public int CurrentHP
    {
        get { return instanceHP; }
        set
        {
            instanceHP = value;
            if (instanceHP >= pDATA.maxHP)
                instanceHP = pDATA.maxHP;
            else if (instanceHP < 0)
                instanceHP = 0;

        }
    }
    //TP
    private int instanceTP;
    public int CurrentTP
    {
        get { return instanceTP; }
        set
        {
            instanceTP = value;
            if (instanceTP >= pDATA.maxTP)
                instanceTP = pDATA.maxTP;
            else if (instanceTP < 0)
                instanceTP = 0;
        }
    }
    //Move
    [HideInInspector]
    public float moveInputValue;
    private float currentMoveSpeed;
    //Jump
    private bool isGrounded;
    //Attack
    private bool attackCooltimeState = false;

    private void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        characterSprite = GetComponent<SpriteRenderer>();
        
        SetStartUpSetting();
    }
    
    private void FixedUpdate()
    {
        if (stopOn)
            return;
        if (isInvincible)
            return;

        Move();
        Jump();
        Attack();

        RecoveryTP();
    }

    private void LateUpdate()
    {
        SetAnimationState();
    }

    #region Move
    /// <summary>
    /// NOTE : 플레이어 캐릭터 이동 함수
    /// NOTE : 최대 속도 -> 키 입력값 * maxSpeedValue (프레임당 키입력 값은 (0~1))
    /// FIXME : rb2D.velocity.y 플레이어가 떨어지면서 땅에 안착했을때 0이 되지않고 -5.0938같은 값으로 처리될 때가 있어서 임시 방편으로 (int)형으로 캐스팅 수정
    /// </summary>
    private void Move()
    {
        float maxspeed = moveInputValue * pDATA.maxSpeedValue;
        currentMoveSpeed = AccelerationCalc(currentMoveSpeed, maxspeed);
        rb2D.velocity = new Vector2(currentMoveSpeed, rb2D.velocity.y);
        //캐릭터의 방향 설정
        if (!moveInputValue.Equals(0f))
            transform.localEulerAngles = moveInputValue > 0f ? new Vector3(0f, 0f, 0f) : new Vector3(0f, 180f, 0f);
    }
    /// <summary>
    /// NOTE : 가속도 [ 현재속도 = 현재속도 + 가속값 * 방향 * Time]
    /// NOTE : 가속할떄 가속력과 감속할때 감속력이 다르다
    /// </summary>
    /// <param name="currentspeed"></param>
    /// <param name="maxspeed"></param>
    /// <returns></returns>
    private float AccelerationCalc(float currentspeed, float maxspeed)
    {
        if (currentspeed.Equals(maxspeed))
            return currentspeed;
        
        float dir = Mathf.Sign(maxspeed - currentspeed);
        float accelrate = maxspeed.Equals(0f) ? pDATA.decelerationValue : pDATA.accelerationValue;
        currentspeed += dir * accelrate * Time.deltaTime;
        return (dir.Equals(Mathf.Sign(maxspeed - currentspeed))) ? currentspeed : maxspeed;
    }
    #endregion

    #region JUMP
    /// <summary>
    /// NOTE : 코루틴 함수가 현재 실행중인지 체크한 후 실행
    /// NOTE : IsGrounded는 캐릭터 오브젝트의 자식오브젝트의 트리거함수로 설정됨
    /// </summary>
    private void Jump()
    {
        if (jumpButtonPress)
        {
            if (isGrounded)
            {
                if (((int)rb2D.velocity.y).Equals(0))
                    //GroundCheck && isrunningjumpcoroutine의 경우에는 2번 점프 되는경우가 생겨서 묶음)
                    if (!isRunningJumpCoroutine)
                        StartCoroutine(JumpCoroutine());
            }
        }
    }
    /// <summary>
    /// NOTE : JUMP()함수 실행되는 Coroutine
    /// TODO : 버튼이나 해당 키가 눌려있음을 체크하여 JumpCount만큼 실행, 다른 방법으로도 구현할 가능성이 있음
    /// </summary>
    private IEnumerator JumpCoroutine()
    {
        isRunningJumpCoroutine = true;
        float jumpCount = 0f;
        while (jumpButtonPress && jumpCount <= pDATA.jumpMaxCount)
        {
            jumpCount++;
            rb2D.AddForce(Vector2.up * pDATA.jumpPowerPerCount, ForceMode2D.Impulse);
            yield return new WaitForSeconds(pDATA.addForceFrameIntervalTime);
        }
        isRunningJumpCoroutine = false;
    }
    #endregion

    #region ATTACK 
    private void Attack()
    {
        //버튼 입력
        if (attackButtonPress)
        {
            //쿨타임
            if (!attackCooltimeState)
            {
                //TP
                if (CurrentTP >= pDATA.attackTPAmount)
                    attackOn = true;
                else
                {
                    attackOn = false;
                    Debug.Log("기력이부조카당");
                }
            }
            else
            {
                attackOn = false;
                Debug.Log("쿨이당");
            }
        }
        else
        {
            attackOn = false;
        }
    }

    //(ANIMATION ADD EVENT FUNCTION)
    /// <summary>
    /// NOTE : 자식으로 저장된 attackEffectModel을 on, off하는 형식
    /// NOTE : ATTACK ANIMATION 클립 내부 add Event에서 출력
    /// TODO : 현재 애니매이션 클립내부에서 실행되기 때문에 클립이 변경된다면 함수 선언위치 등 고려가능성이 높음
    /// </summary>
    public void AttackEffectOn()
    {
        UseUpTP(pDATA.attackTPAmount);
        pDATA.attackEffectModel.SetActive(true);
    }
    public void AttackEffectOff()
    {
        pDATA.attackEffectModel.SetActive(false);
    }
    /// <summary>
    /// NOTE : ATTACK 상태 실행시 코루틴
    /// NOTE : ATTACK ANIMATION 클립 내부 add Event에서 출력 
    /// NOTE : 공격 실행 후 카운트 실행 설정한ATTACK COOLTIME값 이후 ATTACK Possible true로 변경
    /// </summary>
    /// <returns></returns>
    public IEnumerator AttackCoroutine()
    {
        isRunningAttackCoroutine = true;
        attackCooltimeState = true;
        yield return new WaitForSeconds(pDATA.attackCoolTime);
        attackCooltimeState = false;
        isRunningAttackCoroutine = false;
    }
    #endregion

    #region STOP
    /// <summary>
    /// NOTE : STOP코루틴 함수 실행
    /// NOTE : 외부 함수에서 실행하는 경우가 있어서 public 선언
    /// </summary>
    /// <param name="stoptime"></param>
    public void StopCharacter(float stoptime)
    {
        jumpButtonPress = false;
        attackButtonPress = false;
        currentMoveSpeed = 0f;
        rb2D.velocity = new Vector2(0f, 0f);
        StartCoroutine(StopCoroutine(stoptime));
    }

    /// <summary>
    /// NOTE : 파라미터 시간 만큼 TIME STOP
    /// </summary>
    /// <param name="stoptime"></param>
    /// <returns></returns>
    private IEnumerator StopCoroutine(float stoptime)
    {
        isRunningStopCoroutine = true;
        stopOn = true;
        yield return new WaitForSeconds(stoptime);
        stopOn = false;
        isRunningStopCoroutine = false;
    }
    #endregion
    
    #region POINT UI
    /// <summary>
    /// NOTE : HP UI UPDATE
    /// </summary>
    private void SetHPUI()
    {
        HPSlider.value = CurrentHP;
        HPText.text = CurrentHP.ToString();
    }
    /// <summary>
    /// NOTE : TP UI UPDATE
    /// </summary>
    private void SetTPUI()
    {
        TPSlider.value = CurrentTP;
        TPText.text = CurrentTP.ToString();
    }
    #endregion

    #region TP
    /// <summary>
    /// NOTE : 파라미터값만큼 tp 감소
    /// </summary>
    /// <param name="amount"></param>
    private void UseUpTP(int amount)
    {
        CurrentTP -= amount;
    }
    /// <summary>
    /// NOTE : TP 코루틴 실행
    /// </summary>
    private void RecoveryTP()
    {
        if (CurrentTP < pDATA.maxTP)
        {
            if (!isRunningRecoverTPCoroutine)
                StartCoroutine(RecoveryTPCoroutine());
        }

    }
    /// <summary>
    /// maxTP 보다 TP가 낮을경우 반복
    /// </summary>
    /// <returns></returns>
    IEnumerator RecoveryTPCoroutine()
    {
        isRunningRecoverTPCoroutine = true;
        while (CurrentTP < pDATA.maxTP)
        {
            CurrentTP += pDATA.recoveryTPAmount;
            SetTPUI();
            yield return new WaitForSeconds(pDATA.recoveryTPRate);
        }
        isRunningRecoverTPCoroutine = false;
    }
    #endregion


    /// <summary>
    /// NOTE : 데미지
    /// </summary>
    /// <param name="damage"></param>
    private void TakeDamage(int damage, Transform target)
    {
        if (isInvincible)
            return;

        CurrentHP -= damage;
        SetHPUI();

        //Knockback Action
        rb2D.velocity = Vector2.zero;
        float xdir = Mathf.Sign(transform.position.x - target.position.x);
        Vector2 dir = new Vector2(xdir, 1f);
        rb2D.AddForce(dir * pDATA.knockBackPower, ForceMode2D.Impulse);
        //무적상태 시작
        StartCoroutine(InvincibilityCoroutine());
    }

    /// <summary>
    /// NOTE : 설정한 무적시간만큼 상태 설정 및 캐릭터 스프라이트 점멸
    /// </summary>
    /// <returns></returns>
    IEnumerator InvincibilityCoroutine()
    {
        isInvincible = true;
        float timer = 0f;
        Color tmpcolor = characterSprite.color;
        while(timer<=pDATA.invincibleTime)
        {
            timer += pDATA.flashRate;
            tmpcolor.a = tmpcolor.a.Equals(1f) ? 0.2f : 1f;
            characterSprite.color = tmpcolor;

            yield return new WaitForSeconds(pDATA.flashRate);
        }
        tmpcolor.a = 1f;
        characterSprite.color = tmpcolor;
        isInvincible = false;
    }

    /// <summary>
    /// 캐릭터 상태 설정
    /// NOTE : STATE 우선 순위
    /// 1. ATTACK 2. JUMP 3. FALL 4. MOVE
    /// </summary>
    private void SetAnimationState()
    {
        //캐릭터 상태 설정(애니매이션 상태 설정)
        if ((int)rb2D.velocity.y > 0)
            CurrentPlayerAnimState = ANIMATION_STATE.Jump;
        else if ((int)rb2D.velocity.y < 0)
            CurrentPlayerAnimState = ANIMATION_STATE.Fall;
        else
            CurrentPlayerAnimState = (int)(currentMoveSpeed * 10) == 0 ? ANIMATION_STATE.Idle : ANIMATION_STATE.Walk;

        if (attackOn)
            CurrentPlayerAnimState = ANIMATION_STATE.Attack;

        anim.SetFloat("StateFloat", (int)CurrentPlayerAnimState);
    }

    /// <summary>
    /// NOTE : HP, TP UI의 경우에는 자신이 참조하지 않고 player에서 참조 
    /// TODO : HP, TP 자체에서 해결하게 되면 스크립트가 추가될 뿐만 아니라 PLAYER 를 참조하고 UPDATE함수에서 지속적으로 검색해주어야 하기 떄문
    /// </summary>
    private void SetStartUpSetting()
    {
        HPSlider = GameObject.Find("HPSlider").GetComponent<Slider>();
        HPText = GameObject.Find("HPText").GetComponent<Text>();
        TPSlider = GameObject.Find("TPSlider").GetComponent<Slider>();
        TPText = GameObject.Find("TPText").GetComponent<Text>();

        HPSlider.maxValue = pDATA.maxHP;
        TPSlider.maxValue = pDATA.maxTP;

        CurrentHP = pDATA.maxHP;
        CurrentTP = pDATA.maxTP;

        SetHPUI();
        SetTPUI();

    }

    #region COLLISION
    /// <summary>
    /// EnterDamage
    /// </summary>
    /// <param name="collision"></param>
    private void OnCollisionEnter2D(Collision2D collision)
    {

    }

    /// <summary>
    /// NOTE : FloorCheck
    /// </summary>
    /// <param name="collision"></param>
    private void OnCollisionStay2D(Collision2D collision)
    {
        //인접한 collision 노말벡터
        Vector2 contactnormalSum = Vector2.zero;
        for (int i = 0; i < collision.contactCount; i++)
            contactnormalSum += collision.contacts[i].normal;

        if (contactnormalSum.y > 0)
            isGrounded = true;

        //적과 인접했을때
        if (collision.collider.CompareTag("Enemy"))
            TakeDamage(collision.collider.GetComponent<Monster>().mDATA.bodyAttacktDamage, collision.transform);
    }

    /// <summary>
    /// Exit처리가 하나라도되면 IsGrounded 체크
    /// TODO : EXIT되는 contact가 표시되지 않고 collider tag는 체크되나
    /// floor태그에 isgrounded를 false하게되면 옆면에서 부딪힌 floor에서 떨어질떄도 체크가 해제됨
    /// 또한 floor뿐만아니라 다른 오브젝트에서도 점프를 가능하게 해야하기때문에 현재 무언가 위에 존재하고있는지 체크가 가능하다.
    /// XXX : 무언가 조건을 걸어 바닥만 떨어지는 것을 체크하는 부분 고려
    /// </summary>
    /// <param name="collision"></param>
    private void OnCollisionExit2D(Collision2D collision)
    {
        isGrounded = false;
    }
    #endregion
}